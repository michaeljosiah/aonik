using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Fulfilment;
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
    private readonly GuestOrderAccess _guestOrders;
    private readonly IFulfilmentPromiseService _fulfilment;

    public CheckoutService(
        CommerceDbContext dbContext,
        IInventoryService inventory,
        IOrderService orders,
        IPaymentInitiator payments,
        IInvoiceWriter invoices,
        IDiscountService discounts,
        ITaxCalculator tax,
        ITenantProvider tenantProvider,
        IBoxCheckoutSupport boxCheckout,
        GuestOrderAccess guestOrders,
        IFulfilmentPromiseService fulfilment)
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
        _guestOrders = guestOrders;
        _fulfilment = fulfilment;
    }

    private static readonly JsonSerializerOptions EnvelopeSerializerOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>The ItemType of a materialised delivery charge (Spec 068 §9) — excluded from
    /// goods-discount apportionment in reporting.</summary>
    internal const string DeliveryFeeItemType = "DeliveryFee";

    public async Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        CartTracking.Detach(_dbContext, tenantId, command.CartId);
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
                    prior.PaymentClientSecret, prior.PaymentCheckoutUrl,
                    GuestOrderToken(cart));
            }
        }

        CartWriteGuard.RequireCurrent(cart, access);
        var draft = CartDraftData.Read(cart);
        if (draft?.Gift is { GiftIntent: true })
            throw new StorefrontValidationException("Gift fulfilment is not yet available for checkout.");
        var requestedDelivery = command.Delivery ?? CartDraftData.Delivery(draft);

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

        // Dedicated boxes ship; generic Commerce clients can still sell nonshipping goods.
        // Replay above deliberately preserves the original snapshot even after calendar edits.
        if (requestedDelivery is null && cart.BoxBundleProductId is not null)
            throw new StorefrontValidationException("Delivery details are required for a box checkout.");
        OrderDeliveryDto? delivery = null;
        if (requestedDelivery is { } submittedDelivery)
        {
            var details = CheckoutDeliveryValidator.NormalizeAndValidate(submittedDelivery);
            var selected = await _fulfilment.ValidateDeliveryDateAsync(details.DeliveryDate, cancellationToken);
            delivery = new OrderDeliveryDto(details.Purchaser, details.Address, selected.DeliveryDate,
                selected.Timezone, details.Recipient ?? new DeliveryRecipientDto(
                    $"{details.Purchaser.FirstName} {details.Purchaser.LastName}", details.Purchaser.Phone), details.Notes);
        }

        // Spec 068 §9 — a box cart re-validates everything BEFORE reservation: drift stops the
        // checkout with a 409 carrying the refreshed box (A18); an incomplete box rejects (R8).
        BoxCheckoutShape? box;
        try
        {
            box = cart.BoxBundleProductId is not null
                ? await _boxCheckout.PrepareForCheckoutAsync(cart, cancellationToken)
                : null;
        }
        catch
        {
            CartTracking.Detach(_dbContext, tenantId, cart.Id);
            throw;
        }

        // The whole charge breakdown is computable from the cart alone, so it runs BEFORE any
        // durable side effect: a nonpositive payable (e.g. a 100% coupon with zero delivery)
        // must reject while there is still nothing to unwind (L4).
        var subtotal = box is not null ? box.GoodsTotal + box.AddOnGoodsTotal : cart.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity);
        if (box is not null && subtotal <= 0)
        {
            // R4 — a delivery charge must not carry a nonpositive goods figure over the final
            // total guard and mint a negative-priced retail item.
            throw new StorefrontValidationException(
                "The goods total for this box is zero or below; it cannot be checked out.");
        }
        var discount = await _discounts.ComputeAsync(command.DiscountCode ?? draft?.DiscountCode, subtotal, cart.Currency, cancellationToken);
        var taxable = subtotal - discount.Amount;
        var tax = await _tax.CalculateAsync(taxable, cart.Currency, cancellationToken);
        var total = taxable + tax + (box?.DeliveryCharged ?? 0m);
        if (total <= 0)
        {
            throw new StorefrontValidationException(
                "The payable total for this cart is zero or below; it cannot be checked out.");
        }

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
            // Spec 071 X7 — one ordinary retail item per AddOn line, the spine's existing
            // shape; the §12 envelope rides DetailsJson when personalised.
            var nextIndex = 1;
            foreach (var (line, priced, chargedUnit) in box.AddOnLines)
            {
                orderItems.Add(new OrderItemCommand(
                    ItemType: OrderTypeCodes.ProductPurchase,
                    ItemIndex: nextIndex++,
                    AmountIn: chargedUnit * line.Quantity,
                    CurrencyIn: cart.Currency,
                    Quantity: line.Quantity,
                    UnitPrice: chargedUnit,
                    ProductId: line.ProductVariantId,
                    Sku: line.Sku,
                    DetailsJson: priced is null ? null : JsonSerializer.Serialize(priced, EnvelopeSerializerOptions)));
            }

            if (box.DeliveryCharged > 0)
            {
                // Materialised, not absorbed — without this the customer would be charged less
                // than the authoritative quote. Dormant while the setting is zero.
                orderItems.Add(new OrderItemCommand(
                    ItemType: DeliveryFeeItemType,
                    ItemIndex: nextIndex,
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

        // 3. Optionally raise an invoice (when a Finance customer account is supplied).
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
            if (box is not null)
            {
                // Add-ons are ordinary retail and may show their prices (Spec 071 §7).
                lines.AddRange(box.AddOnLines.Select(a =>
                    new InvoiceLineSpec(a.Line.NameSnapshot, a.Line.Quantity, a.ChargedUnitPrice)));
            }
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

        // 4. Initiate funding for the payable total via the permission-free guest path, and link it
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

        // Stage immutable selections only after external work succeeds, beside the final cart claim.
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
                    PersonalisationSummary = BoxCartService.TruncateSummary(priced.Summary),
                    PersonalisationAdjustment = priced.Adjustment,
                    UnitSurcharge = priced.UnitSurcharge ?? 0m,
                    PersonalisationEnvelopeJson = JsonSerializer.Serialize(priced, EnvelopeSerializerOptions),
                });
            }
        }

        // 6. Record the durable charge breakdown + the order on the cart; the cart closes when
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
        if (delivery is not null)
            _dbContext.OrderDeliveryDetails.Add(OrderDeliveryMapper.Create(tenantId, order.Id, delivery));
        cart.OrderId = order.Id;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Inventory shares this context in production. Never let its cleanup SaveChanges
            // flush this attempt's rejected cart claim or order snapshots a second time.
            DiscardAttemptedCheckoutWrites(cart, order.Id);

            // Who won the cart row? Two checkouts share the SAME order via the cart-scoped
            // idempotency key — if the fresh cart already carries THIS order id, the other
            // request was a checkout, its claim stands, and cancelling "our" order would cancel
            // the winner's (L1). Replay its recorded result instead.
            var fresh = await _dbContext.Carts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cart.Id && c.TenantId == tenantId, cancellationToken);
            if (fresh?.OrderId == order.Id)
            {
                if (!CartAccess.IsAuthorized(fresh, access))
                    throw new NotFoundException($"Cart '{cart.Id}' was not found.");
                var recorded = await _dbContext.OrderChargeSummaries.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OrderId == order.Id && s.TenantId == tenantId, cancellationToken);
                var deliveryRecorded = fresh.BoxBundleProductId is null || await _dbContext.OrderDeliveryDetails.AsNoTracking()
                    .AnyAsync(d => d.TenantId == tenantId && d.OrderId == order.Id, cancellationToken);
                if (recorded is not null && deliveryRecorded)
                {
                    // This request's extra unfunded intent expires on its own; the winner's hold,
                    // summary and selection rows are the durable truth.
                    return new CheckoutResult(
                        order.Id, recorded.InvoiceId, recorded.PaymentIntentId, recorded.PaymentStatus,
                        recorded.Subtotal, recorded.DiscountTotal, recorded.TaxTotal, recorded.Total,
                        recorded.Currency, recorded.PaymentClientSecret, recorded.PaymentCheckoutUrl,
                        GuestOrderToken(fresh));
                }
                // A claimed but incomplete winner is an integrity failure; never cancel it or
                // replace its delivery details using this losing request.
                throw;
            }

            // The unique delivery index may reject an insert before the rowversion UPDATE.
            // Without a complete winner, an ordinary database failure must remain a failure.
            if (exception is not DbUpdateConcurrencyException) throw;

            // K4 — a cart EDIT committed between validation and this claim: the created order no
            // longer describes the cart. Unwind the durable side effects (the summary and
            // selection rows roll back with this failed save) and surface the mapped 409 — a
            // resubmit checks out the edited cart, and the replay guard never engages because
            // OrderId was never stamped. The unfunded payment intent expires on its own.
            await _inventory.ReleaseAsync(cart.Id, cancellationToken);
            await _orders.TransitionAsync(order.Id, OrderStatusCodes.Cancelled,
                "Checkout lost the cart claim to a concurrent edit.", cancellationToken: cancellationToken);
            throw;
        }

        await _discounts.MarkRedeemedAsync(discount.DiscountId, cancellationToken);

        return new CheckoutResult(
            order.Id, invoiceId, intent.PaymentIntentId, intent.Status,
            subtotal, discount.Amount, tax, total, cart.Currency, intent.ClientSecret, intent.CheckoutUrl,
            GuestOrderToken(cart));
    }

    private void DiscardAttemptedCheckoutWrites(Entities.Cart.Cart cart, Guid orderId)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added
                     && (entry.Entity is OrderDeliveryDetails delivery && delivery.OrderId == orderId && delivery.TenantId == cart.TenantId
                         || entry.Entity is OrderChargeSummary charge && charge.OrderId == orderId && charge.TenantId == cart.TenantId
                         || entry.Entity is OrderBundleSelection selection && selection.OrderId == orderId && selection.TenantId == cart.TenantId)).ToList())
            entry.State = EntityState.Detached;
        CartTracking.Detach(_dbContext, cart.TenantId, cart.Id);
    }

    private string? GuestOrderToken(Entities.Cart.Cart cart)
        => cart.BuyerPartyId is null && cart.OrderId is { } orderId
            ? _guestOrders.Issue(cart.TenantId, orderId)
            : null;

    public async Task ConfirmPaymentAsync(Guid orderId, Guid? completedPaymentIntentId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts
            .FirstOrDefaultAsync(c => c.OrderId == orderId && c.TenantId == tenantId, cancellationToken);
        if (cart is null)
        {
            return; // not a Commerce checkout order.
        }

        // Only the recorded checkout intent can commit stock, close the cart or complete its order.
        var summary = await _dbContext.OrderChargeSummaries
            .FirstOrDefaultAsync(s => s.OrderId == orderId && s.TenantId == tenantId, cancellationToken);
        if (summary is null || completedPaymentIntentId is not { } completedIntent
            || summary.PaymentIntentId != completedIntent) return;

        // An email retry may arrive after a refund/cancellation. It must not restore captured state.
        if (cart.Status == CartStatuses.CheckedOut && summary.PaymentStatus != CheckoutPaymentStatuses.Captured) return;
        var order = await _orders.GetAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("The checkout order was not found.");
        if (order.Status is not (OrderStatusCodes.Draft or "PendingFunding" or OrderStatusCodes.Complete)) return;

        if (summary.PaymentStatus != CheckoutPaymentStatuses.Captured)
        {
            summary.PaymentStatus = CheckoutPaymentStatuses.Captured;
        }

        // Commit inventory + close the cart exactly once; guarded by cart status so an outbox retry
        // doesn't double-commit stock.
        if (cart.Status != CartStatuses.CheckedOut)
        {
            await _inventory.CommitAsync(cart.Id, cancellationToken);
            cart.Status = CartStatuses.CheckedOut;
        }
        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Retry a partial completion, but never overwrite an intervening order transition.
        if (order.Status != OrderStatusCodes.Complete)
            await _orders.TransitionAsync(orderId, OrderStatusCodes.Complete, "Payment completed",
                expectedFromStatus: order.Status, cancellationToken: cancellationToken);
    }
}
