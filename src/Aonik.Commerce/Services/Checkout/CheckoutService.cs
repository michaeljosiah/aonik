using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Checkout orchestration over the Commerce + Ordering + Finance seams (Spec 042 §11/§12).</summary>
internal sealed class CheckoutService : ICheckoutService
{
    private readonly CommerceDbContext _dbContext;
    private readonly IInventoryService _inventory;
    private readonly IOrderService _orders;
    private readonly IPaymentInitiator _payments;
    private readonly IInvoiceWriter _invoices;
    private readonly IDiscountService _discounts;
    private readonly ITaxCalculator _tax;
    private readonly ITenantProvider _tenantProvider;

    public CheckoutService(
        CommerceDbContext dbContext,
        IInventoryService inventory,
        IOrderService orders,
        IPaymentInitiator payments,
        IInvoiceWriter invoices,
        IDiscountService discounts,
        ITaxCalculator tax,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _inventory = inventory;
        _orders = orders;
        _payments = payments;
        _invoices = invoices;
        _discounts = discounts;
        _tax = tax;
        _tenantProvider = tenantProvider;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var cart = await _dbContext.Carts
            .Include(c => c.Items).ThenInclude(i => i.Selections)
            .FirstOrDefaultAsync(c => c.Id == command.CartId && c.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Cart '{command.CartId}' was not found.");

        // Idempotency: a cart stays Open until payment completes, so a retry / double-click re-enters
        // here. If checkout already ran (OrderId stamped), replay the recorded result rather than
        // reserving stock or creating the order/payment again.
        if (cart.OrderId is { } existingOrderId)
        {
            var prior = await _dbContext.OrderChargeSummaries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == existingOrderId && s.TenantId == tenantId, cancellationToken);
            if (prior is not null)
            {
                return new CheckoutResult(
                    existingOrderId, prior.InvoiceId, prior.PaymentIntentId, prior.PaymentStatus,
                    prior.Subtotal, prior.DiscountTotal, prior.TaxTotal, prior.Total, prior.Currency);
            }
        }

        if (cart.Status != CartStatuses.Open)
        {
            throw new InvalidOperationException($"Cart '{cart.Id}' is {cart.Status}, not Open.");
        }
        if (cart.Items.Count == 0)
        {
            throw new InvalidOperationException("Cannot check out an empty cart.");
        }
        if (string.IsNullOrWhiteSpace(command.Provider))
        {
            throw new ArgumentException("A payment provider is required to check out.", nameof(command));
        }
        if (string.IsNullOrWhiteSpace(command.PaymentMethodType))
        {
            throw new ArgumentException("A payment method type is required to check out.", nameof(command));
        }

        // 1. Reserve stock — fan out bundle lines to their component variants (all-or-nothing).
        var reservationLines = new List<InventoryReservationLine>();
        foreach (var item in cart.Items)
        {
            if (item.IsBundle)
            {
                foreach (var sel in item.Selections)
                {
                    reservationLines.Add(new InventoryReservationLine(sel.ProductVariantId, sel.Quantity * item.Quantity));
                }
            }
            else
            {
                reservationLines.Add(new InventoryReservationLine(item.ProductVariantId, item.Quantity));
            }
        }
        await _inventory.ReserveAsync(cart.Id, reservationLines, cancellationToken);

        // 2. Create the ProductPurchase order (idempotent on the cart so a double-submit is safe).
        var orderItems = new List<OrderItemCommand>();
        var bundleLineIndices = new List<(int Index, CartItem Item)>();
        var index = 0;
        foreach (var item in cart.Items)
        {
            orderItems.Add(new OrderItemCommand(
                ItemType: OrderTypeCodes.ProductPurchase,
                ItemIndex: index,
                AmountIn: item.UnitPriceSnapshot * item.Quantity,
                CurrencyIn: cart.Currency,
                Quantity: item.Quantity,
                UnitPrice: item.UnitPriceSnapshot,
                ProductId: item.IsBundle ? item.BundleProductId : item.ProductVariantId,
                Sku: item.Sku));
            if (item.IsBundle)
            {
                bundleLineIndices.Add((index, item));
            }
            index++;
        }

        var order = await _orders.CreateAsync(new CreateOrderCommand(
            OrderType: OrderTypeCodes.ProductPurchase,
            PayerPartyId: cart.BuyerPartyId,
            CurrencyIn: cart.Currency,
            Items: orderItems,
            IdempotencyKey: $"cart:{cart.Id:N}"), cancellationToken);

        // 3. Record build-your-own-box contents (Option A — Commerce-owned, soft-linked to the order).
        foreach (var (lineIndex, item) in bundleLineIndices)
        {
            foreach (var sel in item.Selections)
            {
                _dbContext.OrderBundleSelections.Add(new OrderBundleSelection
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = order.Id,
                    OrderItemIndex = lineIndex,
                    BundleSlotId = sel.BundleSlotId,
                    ProductVariantId = sel.ProductVariantId,
                    Quantity = sel.Quantity * item.Quantity,
                    Sku = sel.Sku,
                });
            }
        }

        // 4. Compute the charge breakdown. The order lines stay the goods (subtotal); discount + tax
        //    are payment-side, so Order / Payment / Ledger stay distinct.
        var subtotal = order.AmountIn;
        var discount = await _discounts.ComputeAsync(command.DiscountCode, subtotal, cart.Currency, cancellationToken);
        var taxable = subtotal - discount.Amount;
        var tax = await _tax.CalculateAsync(taxable, cart.Currency, cancellationToken);
        var total = taxable + tax;

        // 5. Optionally raise an invoice (when a Finance customer account is supplied).
        Guid? invoiceId = null;
        if (command.CustomerAccountId is { } customerAccountId)
        {
            var lines = cart.Items
                .Select(i => new InvoiceLineSpec(i.NameSnapshot, i.Quantity, i.UnitPriceSnapshot))
                .ToList();
            if (discount.Amount > 0)
            {
                lines.Add(new InvoiceLineSpec($"Discount ({discount.Code})", 1m, -discount.Amount));
            }
            if (tax > 0)
            {
                lines.Add(new InvoiceLineSpec("Tax", 1m, tax));
            }
            var invoice = await _invoices.CreateForOrderAsync(
                new CreateInvoiceForOrderCommand(order.Id, customerAccountId, cart.Currency, lines), cancellationToken);
            invoiceId = invoice.InvoiceId;
        }

        // 6. Initiate funding for the payable total via the permission-free guest path, and link it
        //    to the order. Capture stays a Finance high-tier action.
        var intent = await _payments.CreateGuestIntentForOrderAsync(new CreateGuestPaymentIntentForOrderCommand(
            OrderId: order.Id,
            Amount: total,
            Currency: cart.Currency,
            Provider: command.Provider,
            PaymentMethodType: command.PaymentMethodType,
            ReturnUrl: command.ReturnUrl,
            CancelUrl: command.CancelUrl), cancellationToken);

        await _orders.LinkFundingAsync(order.Id, intent.PaymentIntentId, cancellationToken);

        // 7. Record the durable charge breakdown + the order on the cart; the cart closes when
        //    payment completes (ConfirmPaymentAsync).
        _dbContext.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            Currency = cart.Currency,
            Subtotal = subtotal,
            DiscountTotal = discount.Amount,
            DiscountCode = discount.Code,
            TaxTotal = tax,
            Total = total,
            PaymentIntentId = intent.PaymentIntentId,
            InvoiceId = invoiceId,
            PaymentStatus = intent.Status,
        });
        cart.OrderId = order.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _discounts.MarkRedeemedAsync(discount.DiscountId, cancellationToken);

        return new CheckoutResult(
            order.Id, invoiceId, intent.PaymentIntentId, intent.Status,
            subtotal, discount.Amount, tax, total, cart.Currency, intent.ClientSecret, intent.CheckoutUrl);
    }

    public async Task ConfirmPaymentAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts
            .FirstOrDefaultAsync(c => c.OrderId == orderId && c.TenantId == tenantId, cancellationToken);
        if (cart is null || cart.Status == CartStatuses.CheckedOut)
        {
            return; // not a Commerce checkout order, or already confirmed — idempotent.
        }

        await _inventory.CommitAsync(cart.Id, cancellationToken);
        cart.Status = CartStatuses.CheckedOut;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _orders.TransitionAsync(orderId, "Complete", "Payment completed", cancellationToken);
    }
}
