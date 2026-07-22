using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions;
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
    private readonly IBoxCheckoutSupport _boxCheckout;

    public CheckoutService(
        CommerceDbContext dbContext,
        IInventoryService inventory,
        IOrderService orders,
        IPaymentInitiator payments,
        IInvoiceWriter invoices,
        IDiscountService discounts,
        ITaxCalculator tax,
        ITenantProvider tenantProvider,
        IBoxCheckoutSupport boxCheckout)
    {
        _dbContext = dbContext;
        _inventory = inventory;
        _orders = orders;
        _payments = payments;
        _invoices = invoices;
        _discounts = discounts;
        _tax = tax;
        _tenantProvider = tenantProvider;
        _boxCheckout = boxCheckout;
    }

    private static readonly JsonSerializerOptions EnvelopeSerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var cart = await _dbContext.Carts
            .Include(c => c.Items).ThenInclude(i => i.Selections)
            .FirstOrDefaultAsync(c => c.Id == command.CartId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Cart '{command.CartId}' was not found.");

        // R10 — money movement begins here; an unauthorized caller gets the same 404 an unknown
        // cart id gets, before the idempotent replay can leak a prior checkout's figures.
        if (!CartAccess.IsAuthorized(cart, access))
        {
            throw new NotFoundException($"Cart '{command.CartId}' was not found.");
        }

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
                    prior.Subtotal, prior.DiscountTotal, prior.TaxTotal, prior.Total, prior.Currency,
                    prior.PaymentClientSecret, prior.PaymentCheckoutUrl);
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

        // Spec 068 §9 — a box cart re-validates everything BEFORE reservation: drift stops the
        // checkout with a 409 carrying the refreshed box (A18); an incomplete box rejects (R8).
        var box = cart.BoxBundleProductId is not null
            ? await _boxCheckout.PrepareForCheckoutAsync(cart, cancellationToken)
            : null;

        // 1. Reserve stock — fan out bundle lines to their component variants (all-or-nothing).
        // First release any held reservations left by a prior attempt that aborted before stamping
        // cart.OrderId, so a retry can't accumulate duplicate holds for the same cart.
        await _inventory.ReleaseAsync(cart.Id, cancellationToken);

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
        if (box is null)
        {
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
        }
        else
        {
            // §9 — one OrderItem per PRICED thing: the box (goods total: box price +
            // personalisation + surcharges — never per-dish prices) and, when charged, the
            // delivery fee. The box envelope rides DetailsJson; per-line facts land on the
            // selection rows below.
            orderItems.Add(new OrderItemCommand(
                ItemType: OrderTypeCodes.ProductPurchase,
                ItemIndex: 0,
                AmountIn: box.GoodsTotal,
                CurrencyIn: cart.Currency,
                Quantity: 1m,
                UnitPrice: box.GoodsTotal,
                ProductId: cart.BoxBundleProductId,
                Sku: box.BundleSku,
                DetailsJson: box.EnvelopeJson));
            if (box.DeliveryCharged > 0)
            {
                // Materialised, not absorbed — without this the customer would be charged less
                // than the authoritative quote. Dormant while the setting is zero.
                orderItems.Add(new OrderItemCommand(
                    ItemType: "DeliveryFee",
                    ItemIndex: 1,
                    AmountIn: box.DeliveryCharged,
                    CurrencyIn: cart.Currency,
                    Quantity: 1m,
                    UnitPrice: box.DeliveryCharged,
                    ProductId: null,
                    Sku: "delivery",
                    DetailsJson: null));
            }
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

        // 3b. Spec 068 §9 — the kitchen-facing landing: one selection row per BoxDish line,
        // carrying the canonical personalisation, its projections, and the immutable §12
        // envelope — after defaults or option prices change, the order still explains which
        // choices produced the amount charged without consulting the live catalogue.
        if (box is not null)
        {
            foreach (var (line, priced) in box.Lines)
            {
                _dbContext.OrderBundleSelections.Add(new OrderBundleSelection
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = order.Id,
                    OrderItemIndex = 0,
                    BundleSlotId = line.BoxBundleSlotId!.Value,
                    ProductVariantId = line.ProductVariantId,
                    Quantity = line.Quantity,
                    Sku = line.Sku,
                    PersonalisationJson = priced.CanonicalSelectionJson,
                    PersonalisationSummary = priced.Summary,
                    PersonalisationAdjustment = priced.Adjustment,
                    UnitSurcharge = priced.UnitSurcharge ?? 0m,
                    PersonalisationEnvelopeJson = JsonSerializer.Serialize(priced, EnvelopeSerializerOptions),
                });
            }
        }

        // 4. Compute the charge breakdown. The order lines stay the goods (subtotal); discount + tax
        //    are payment-side, so Order / Payment / Ledger stay distinct.
        // For a box order the goods subtotal is the box's goods total — order.AmountIn also
        // carries the delivery item, and delivery is neither discountable nor taxable here; it
        // joins the payable total at the end (§9: subtotal − discount + tax + delivery).
        var subtotal = box?.GoodsTotal ?? order.AmountIn;
        var discount = await _discounts.ComputeAsync(command.DiscountCode, subtotal, cart.Currency, cancellationToken);
        var taxable = subtotal - discount.Amount;
        var tax = await _tax.CalculateAsync(taxable, cart.Currency, cancellationToken);
        var total = taxable + tax + (box?.DeliveryCharged ?? 0m);

        // 5. Optionally raise an invoice (when a Finance customer account is supplied).
        Guid? invoiceId = null;
        if (command.CustomerAccountId is { } customerAccountId)
        {
            // A box invoice is box-aggregate (§9): per-CartItem lines would price dishes from
            // snapshots that are explicitly not pricing inputs and disagree with the single
            // aggregate order item. Non-box carts keep the per-item path untouched.
            var lines = box is null
                ? cart.Items
                    .Select(i => new InvoiceLineSpec(i.NameSnapshot, i.Quantity, i.UnitPriceSnapshot))
                    .ToList()
                : new List<InvoiceLineSpec> { new($"{box.Size}-dish box", 1m, box.GoodsTotal) };
            if (box is { DeliveryCharged: > 0 })
            {
                lines.Add(new InvoiceLineSpec("Delivery", 1m, box.DeliveryCharged));
            }
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
            PaymentClientSecret = intent.ClientSecret,
            PaymentCheckoutUrl = intent.CheckoutUrl,
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
        if (cart is null)
        {
            return; // not a Commerce checkout order.
        }

        // Commit inventory + close the cart exactly once; guarded by cart status so an outbox retry
        // doesn't double-commit stock.
        if (cart.Status != CartStatuses.CheckedOut)
        {
            await _inventory.CommitAsync(cart.Id, cancellationToken);
            cart.Status = CartStatuses.CheckedOut;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Always ensure the order reaches Complete — TransitionAsync is a no-op when already there,
        // so an outbox retry after a transition failure (cart already CheckedOut) still completes it
        // rather than leaving the order stuck in PendingFunding. Deliberately no expectedFromStatus:
        // this is an unconditional converge-to-Complete, not a guarded transition.
        await _orders.TransitionAsync(orderId, "Complete", "Payment completed", cancellationToken: cancellationToken);
    }
}
