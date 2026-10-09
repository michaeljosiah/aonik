using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
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
using Aonik.SharedKernel.Persistence;

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
    private readonly IDeliveryCoverageService _coverage;
    private readonly IPartyService _parties;
    private readonly IClock _clock;

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
        IFulfilmentPromiseService fulfilment,
        IDeliveryCoverageService coverage, IPartyService parties, IClock clock)
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
        _coverage = coverage;
        _parties = parties;
        _clock = clock;
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

        // Durable agent approval binds the original editable version, not an unrelated newer attempt.
        if (command.RequireFreshCart) CartWriteGuard.RequireCurrent(cart, access);
        if (cart.CheckoutState is CartCheckoutStates.Preparing or CartCheckoutStates.AwaitingPayment)
            return await ResumeAsync(cart.Id, CheckoutPreparation.Read(cart), access, cancellationToken);
        if (cart.OrderId is { } existingOrderId && cart.CheckoutState != CartCheckoutStates.Retryable)
            return await ReplayAsync(cart, existingOrderId, cancellationToken);
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
            if (cart.BoxBundleProductId is not null)
            {
                if (details.Address.CountryCode != "GB")
                    throw new DeliveryCoverageException(DeliveryCoverageException.UnsupportedCountry,
                        "Box delivery currently requires a GB address.", "countryCode");

                var coverage = await _coverage.CheckAsync(details.Address.Postcode, cancellationToken);
                if (coverage.Status == DeliveryCoverageStatuses.NotServed)
                    throw new DeliveryCoverageException(DeliveryCoverageException.NotServed,
                        "Delivery is not available to this postcode.", "postcode");
                if (coverage.Status != DeliveryCoverageStatuses.Serves)
                    throw new DeliveryCoverageException(DeliveryCoverageException.Unavailable,
                        "Delivery coverage could not be checked. Please try again.");

                details = details with
                {
                    Address = details.Address with { Postcode = coverage.NormalisedPostcode ?? details.Address.Postcode }
                };
            }
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

        if (!string.Equals(command.Provider, "Stripe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(command.PaymentMethodType, "Card", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(cart.Currency, "GBP", StringComparison.OrdinalIgnoreCase)
            || total < 0.30m || total > 999999.99m || decimal.Truncate(total * 100m) != total * 100m)
            throw new StorefrontValidationException("Checkout supports Stripe card payments in GBP, in whole pennies from 0.30 to 999999.99.");

        // Freeze the authoritative stock and order inputs before claiming checkout.
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

        if (cart.BuyerPartyId is null && delivery is null)
            throw new StorefrontValidationException("Purchaser contact details are required for guest payment.");
        if (cart.CheckoutState == CartCheckoutStates.Retryable && command.CustomerAccountId is not null)
            throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");

        var invoiceLines = box is null
            ? cart.Items.Select(i => new InvoiceLineSpec(i.NameSnapshot, i.Quantity, i.UnitPriceSnapshot)).ToList()
            : new List<InvoiceLineSpec> { new($"{box.Size}-dish box", 1m, box.GoodsTotal) };
        if (box is not null)
            invoiceLines.AddRange(box.AddOnLines.Select(a => new InvoiceLineSpec(a.Line.NameSnapshot, a.Line.Quantity, a.ChargedUnitPrice)));
        if (box is { DeliveryCharged: > 0 }) invoiceLines.Add(new("Delivery", 1m, box.DeliveryCharged));
        if (discount.Amount > 0) invoiceLines.Add(new($"Discount ({discount.Code})", 1m, -discount.Amount));
        if (tax > 0) invoiceLines.Add(new("Tax", 1m, tax));
        var selections = new List<CheckoutSelection>();
        foreach (var (lineIndex, item) in bundleLineIndices)
            selections.AddRange(item.Selections.Select(s => new CheckoutSelection(lineIndex, s.BundleSlotId,
                s.ProductVariantId, s.Quantity * item.Quantity, s.Sku)));
        if (box is not null)
            selections.AddRange(box.Lines.Select(pair => new CheckoutSelection(0, pair.Line.BoxBundleSlotId!.Value,
                pair.Line.ProductVariantId, pair.Line.Quantity, pair.Line.Sku, pair.Priced.CanonicalSelectionJson,
                BoxCartService.TruncateSummary(pair.Priced.Summary), pair.Priced.Adjustment,
                pair.Priced.UnitSurcharge ?? 0m, JsonSerializer.Serialize(pair.Priced, EnvelopeSerializerOptions))));
        Guid? guestPartyId = cart.BuyerPartyId is null
            ? Guid.NewGuid()
            : null;
        var preparation = new CheckoutPreparation(Guid.NewGuid(), guestPartyId, cart.Currency,
            command.Provider, command.PaymentMethodType, command.ReturnUrl, command.CancelUrl, command.CustomerAccountId,
            subtotal, discount.Amount, discount.DiscountId, discount.Code, tax, total, orderItems, invoiceLines,
            reservationLines.Select(line => new CheckoutStockLine(line.Item.Id, line.Quantity)).ToList(), selections, delivery);
        var preparationJson = preparation.Serialize();
        cart.CheckoutState = CartCheckoutStates.Preparing;
        cart.CheckoutPreparationJson = preparationJson;
        CartActivity.ServerEdit(_dbContext, cart, _clock);
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            var winner = await LoadAuthorizedAsync(cart.Id, access, cancellationToken);
            if (command.RequireFreshCart)
                throw new CartWriteConflictException(winner, "commerce.cart_conflict", "The approved cart changed. Read it before proposing checkout again.");
            if (winner.CheckoutState is CartCheckoutStates.Preparing or CartCheckoutStates.AwaitingPayment)
                return await ResumeAsync(winner.Id, CheckoutPreparation.Read(winner), access, cancellationToken);
            if (winner.Status == CartStatuses.CheckedOut && winner.OrderId is { } paidOrder)
                return await ReplayAsync(winner, paidOrder, cancellationToken);
            throw;
        }
        catch
        {
            CartTracking.Detach(_dbContext, tenantId, cart.Id);
            throw;
        }
        return await ResumeAsync(cart.Id, preparation, access, cancellationToken);
    }

    private async Task<CheckoutResult> ResumeAsync(Guid cartId, CheckoutPreparation preparation,
        CartAccessContext access, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (preparation.GuestPartyId is { } guestId)
        {
            var delivery = preparation.Delivery ?? throw new InvalidOperationException("Guest purchaser details are missing.");
            var contact = delivery.Purchaser;
            await _parties.EnsureUnverifiedGuestPartyAsync(guestId, cartId,
                new CreatePartyRequest($"{contact.FirstName} {contact.LastName}", "Person", contact.FirstName,
                    contact.LastName, contact.Phone, contact.Email, delivery.Address.CountryCode), ct);
        }

        // All local preparation is protected by the cart row. Ordering/Invoice writes are
        // independently idempotent; no provider call runs while this SQL transaction is held.
        try
        {
            await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                DetachCheckout(cartId, preparation);
                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token) : null;
                var cart = await LoadAuthorizedAsync(cartId, access, token);
                if (CheckoutPreparation.Read(cart).AttemptId != preparation.AttemptId)
                    throw new CartWriteConflictException(cart, "commerce.cart_conflict", "Checkout changed. Reload its current state.");
                if (cart.CheckoutState != CartCheckoutStates.Preparing) return;
                CartActivity.ServerEdit(_dbContext, cart, _clock);
                await _dbContext.SaveChangesAsync(token); // Claims the parent before touching stock or order rows.
                await _inventory.ReleaseAsync(cart.Id, token);
                await _inventory.ReserveAsync(cart.Id, preparation.Stock.Select(line =>
                    new InventoryReservationLine(line.ProductVariantId, line.Quantity)).ToList(), token);
                var revision = JsonSerializer.Serialize(new { commerceCheckoutAttemptId = preparation.AttemptId });
                var order = await _orders.CreateAsync(new CreateOrderCommand(OrderTypeCodes.ProductPurchase,
                    cart.BuyerPartyId ?? preparation.GuestPartyId, preparation.Currency, preparation.Items,
                    IdempotencyKey: $"cart:{cart.Id:N}", ProvenanceJson: revision), token);
                await _orders.RefreshPendingItemsAsync(order.Id, preparation.AttemptId, cart.BuyerPartyId ?? preparation.GuestPartyId, preparation.Currency, preparation.Items, token);
                cart.OrderId = order.Id;
                Guid? invoiceId = null;
                if (preparation.CustomerAccountId is { } customerId)
                    invoiceId = (await _invoices.CreateForOrderAsync(new CreateInvoiceForOrderCommand(order.Id,
                        customerId, preparation.Currency, preparation.InvoiceLines,
                        IdempotencyKey: $"checkout:{preparation.AttemptId:N}"), token)).InvoiceId;
                var summary = await _dbContext.OrderChargeSummaries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == order.Id, token);
                if (summary is null)
                {
                    summary = new OrderChargeSummary { TenantId = tenantId, OrderId = order.Id };
                    _dbContext.OrderChargeSummaries.Add(summary);
                }
                if (summary.PaymentStatus == CheckoutPaymentStatuses.Captured) throw new InvalidOperationException("A paid checkout is immutable.");
                summary.Currency = preparation.Currency; summary.Subtotal = preparation.Subtotal;
                summary.DiscountTotal = preparation.DiscountTotal; summary.DiscountId = preparation.DiscountId;
                summary.DiscountCode = preparation.DiscountCode; summary.TaxTotal = preparation.TaxTotal; summary.Total = preparation.Total;
                summary.InvoiceId = invoiceId; summary.PaymentIntentId = preparation.AttemptId;
                summary.PaymentStatus = "Pending"; summary.PaymentClientSecret = null; summary.PaymentCheckoutUrl = null;
                var oldSelections = await _dbContext.OrderBundleSelections.Where(s => s.TenantId == tenantId && s.OrderId == order.Id).ToListAsync(token);
                _dbContext.OrderBundleSelections.RemoveRange(oldSelections);
                foreach (var selection in preparation.Selections)
                    _dbContext.OrderBundleSelections.Add(new OrderBundleSelection
                    {
                        TenantId = tenantId, OrderId = order.Id, OrderItemIndex = selection.OrderItemIndex,
                        BundleSlotId = selection.BundleSlotId, ProductVariantId = selection.ProductVariantId,
                        Quantity = selection.Quantity, Sku = selection.Sku, PersonalisationJson = selection.PersonalisationJson,
                        PersonalisationSummary = selection.PersonalisationSummary, PersonalisationAdjustment = selection.PersonalisationAdjustment,
                        UnitSurcharge = selection.UnitSurcharge, PersonalisationEnvelopeJson = selection.PersonalisationEnvelopeJson
                    });
                // Reuse the tenant/order unique row when an unpaid replacement adds or removes
                // optional shipping. Previously omitted snapshots may have been soft deleted.
                var currentDelivery = await _dbContext.OrderDeliveryDetails.IncludeSoftDeleted()
                    .SingleOrDefaultAsync(d => d.TenantId == tenantId && d.OrderId == order.Id, token);
                if (preparation.Delivery is { } delivery)
                {
                    var snapshot = OrderDeliveryMapper.Create(tenantId, order.Id, delivery);
                    if (currentDelivery is null) _dbContext.OrderDeliveryDetails.Add(snapshot);
                    else
                    {
                        snapshot.Id = currentDelivery.Id; snapshot.CreatedAt = currentDelivery.CreatedAt; snapshot.CreatedBy = currentDelivery.CreatedBy;
                        snapshot.RowVersion = currentDelivery.RowVersion;
                        _dbContext.Entry(currentDelivery).CurrentValues.SetValues(snapshot);
                    }
                }
                else if (currentDelivery is { IsDeleted: false }) _dbContext.OrderDeliveryDetails.Remove(currentDelivery);
                cart.OrderId = order.Id;
                cart.CheckoutState = CartCheckoutStates.AwaitingPayment;
                await _dbContext.SaveChangesAsync(token);
                if (transaction is not null) await transaction.CommitAsync(token);
            }, ct);
        }
        catch { DetachCheckout(cartId, preparation); throw; }

        var claimed = await LoadAuthorizedAsync(cartId, access, ct);
        if (CheckoutPreparation.Read(claimed).AttemptId != preparation.AttemptId)
            throw new CartWriteConflictException(claimed, "commerce.cart_conflict", "Checkout changed. Reload its current state.");
        if (claimed.CheckoutState == CartCheckoutStates.Retryable)
            throw new CartWriteConflictException(claimed, "commerce.cart_conflict", "Checkout was cancelled. Reload the cart before trying again.");
        if (claimed.CheckoutState != CartCheckoutStates.AwaitingPayment || claimed.Status == CartStatuses.CheckedOut)
            return await ReplayAsync(claimed, claimed.OrderId ?? throw new InvalidOperationException("Checkout has no order."), ct);
        var orderId = claimed.OrderId!.Value;
        var recordedState = await _payments.GetStateAsync(preparation.AttemptId, ct);
        if (recordedState is not null) ValidatePaymentState(recordedState, orderId, preparation);
        var intent = recordedState is not null && (recordedState.CheckoutUrl is not null || recordedState.CanNoLongerPay
                || recordedState.Status == CheckoutPaymentStatuses.Captured)
            ? new PaymentIntentRef(recordedState.PaymentIntentId, recordedState.Status, CheckoutUrl: recordedState.CheckoutUrl)
            : await _payments.CreateGuestIntentForOrderAsync(new CreateGuestPaymentIntentForOrderCommand(orderId,
                preparation.Total, preparation.Currency, preparation.Provider, preparation.PaymentMethodType,
                preparation.ReturnUrl, preparation.CancelUrl, preparation.AttemptId, $"commerce:{cartId:N}:{preparation.AttemptId:N}"), ct);
        if (intent.PaymentIntentId != preparation.AttemptId) throw new InvalidOperationException("Payment returned a different checkout attempt.");
        await _orders.LinkFundingAsync(orderId, intent.PaymentIntentId, ct);
        var state = await _payments.GetStateAsync(preparation.AttemptId, ct);
        if (state is not null) ValidatePaymentState(state, orderId, preparation);
        claimed = await LoadAuthorizedAsync(cartId, access, ct);
        var charge = await LoadChargeAsync(tenantId, orderId, ct);
        if (charge.PaymentIntentId == preparation.AttemptId && claimed.CheckoutState == CartCheckoutStates.AwaitingPayment
            && charge.PaymentStatus != CheckoutPaymentStatuses.Captured)
        {
            charge.PaymentStatus = state?.Status ?? intent.Status;
            var terminal = state?.CanNoLongerPay == true || charge.PaymentStatus == CheckoutPaymentStatuses.Captured;
            charge.PaymentCheckoutUrl = terminal ? null : state?.CheckoutUrl ?? intent.CheckoutUrl;
            charge.PaymentClientSecret = terminal ? null : intent.ClientSecret ?? charge.PaymentClientSecret;
            // A concurrent completion/recovery wins through the summary's native version.
            try { await _dbContext.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { _dbContext.Entry(charge).State = EntityState.Detached; }
        }
        claimed = await LoadAuthorizedAsync(cartId, access, ct);
        return await ReplayAsync(claimed, orderId, ct);
    }

    public async Task<CartPaymentStateDto> GetPaymentStateAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var cart = await LoadAuthorizedAsync(cartId, access, cancellationToken);
        var preparation = cart.CheckoutPreparationJson is null ? null : CheckoutPreparation.Read(cart);
        var summary = cart.OrderId is { } id ? await _dbContext.OrderChargeSummaries.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == cart.TenantId && s.OrderId == id, cancellationToken) : null;
        var intentId = preparation?.AttemptId ?? summary?.PaymentIntentId;
        var state = intentId is { } paymentId ? await _payments.GetStateAsync(paymentId, cancellationToken) : null;
        if (state is not null && preparation is not null && cart.OrderId is { } orderId)
            ValidatePaymentState(state, orderId, preparation);
        var status = cart.Status == CartStatuses.CheckedOut && summary?.PaymentStatus == CheckoutPaymentStatuses.Captured
            ? "succeeded" : cart.CheckoutState == CartCheckoutStates.Retryable ? "cancelled" : state?.Status switch
            {
                "Failed" => "failed", "Cancelled" => "cancelled", "Expired" => "cancelled",
                "RequiresAction" => "requires_action", _ => "processing"
            };
        return new(cart.OrderId, intentId, status, CartWriteGuard.IsEditable(cart), Convert.ToBase64String(cart.RowVersion),
            state?.CanNoLongerPay == true || state?.Status == CheckoutPaymentStatuses.Captured ? null : state?.CheckoutUrl);
    }

    public async Task<CartPaymentStateDto> RecoverAsync(Guid cartId, Guid expectedPaymentIntentId, CartAccessContext access,
        CancellationToken cancellationToken = default)
    {
        var cart = await LoadAuthorizedAsync(cartId, access, cancellationToken);
        var preparation = cart.CheckoutPreparationJson is null ? null : CheckoutPreparation.Read(cart);
        if (preparation?.AttemptId != expectedPaymentIntentId)
            throw new CartWriteConflictException(cart, "commerce.cart_conflict", "The payment attempt changed. Reload checkout.");
        if (cart.CheckoutState == CartCheckoutStates.Retryable || cart.Status == CartStatuses.CheckedOut)
            return await GetPaymentStateAsync(cartId, access, cancellationToken);
        if (!ActiveBoxCarts.MatchesVersion(cart, access.ExpectedCartVersion))
            throw new CartWriteConflictException(cart, "commerce.cart_conflict", "The cart changed. Reload checkout.");
        if (cart.OrderId is { } invoicedOrder && await _dbContext.OrderChargeSummaries.AsNoTracking()
                .AnyAsync(s => s.TenantId == cart.TenantId && s.OrderId == invoicedOrder && s.InvoiceId != null, cancellationToken))
            throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");
        // Preparing has never entered Finance. Taking the same parent row claim below prevents
        // a delayed local preparation from crossing into AwaitingPayment after this cancellation.
        PaymentIntentStateRef? state = null;
        if (cart.CheckoutState == CartCheckoutStates.AwaitingPayment)
        {
            state = await _payments.GetStateAsync(expectedPaymentIntentId, cancellationToken);
            if (state is null) return await GetPaymentStateAsync(cartId, access, cancellationToken);
            state = await _payments.ExpireAsync(expectedPaymentIntentId, cancellationToken);
            ValidatePaymentState(state, cart.OrderId!.Value, preparation!);
            if (state.Status == CheckoutPaymentStatuses.Captured)
            {
                await ConfirmPaymentAsync(state.OrderId, state.PaymentIntentId, state.Amount, state.Currency, cancellationToken);
                return await GetPaymentStateAsync(cartId, access, cancellationToken);
            }
            if (!state.CanNoLongerPay) return await GetPaymentStateAsync(cartId, access, cancellationToken);
        }
        try
        {
            await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                DetachCheckout(cartId, preparation!);
                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                var current = await LoadAuthorizedAsync(cartId, access, ct);
                if (CheckoutPreparation.Read(current).AttemptId != expectedPaymentIntentId
                    || current.CheckoutState != cart.CheckoutState || current.Status != CartStatuses.Open)
                    throw new CartWriteConflictException(current, "commerce.cart_conflict", "Checkout changed while payment was resolving.");
                if (!ActiveBoxCarts.MatchesVersion(current, access.ExpectedCartVersion))
                    throw new CartWriteConflictException(current, "commerce.cart_conflict", "The cart changed. Reload checkout.");
                current.CheckoutState = CartCheckoutStates.Retryable;
                CartActivity.UserEdit(_dbContext, current, _clock);
                await _dbContext.SaveChangesAsync(ct);
                await _inventory.ReleaseAsync(cartId, ct);
                if (current.OrderId is { } orderId)
                {
                    var summary = await LoadChargeAsync(current.TenantId, orderId, ct);
                    if (summary.PaymentStatus == CheckoutPaymentStatuses.Captured) throw new InvalidOperationException("A paid checkout cannot reopen.");
                    if (summary.InvoiceId is not null) throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");
                    summary.PaymentStatus = state?.Status ?? "Cancelled";
                    summary.PaymentCheckoutUrl = null; summary.PaymentClientSecret = null;
                    await _dbContext.SaveChangesAsync(ct);
                }
                if (transaction is not null) await transaction.CommitAsync(ct);
            }, cancellationToken);
        }
        catch { DetachCheckout(cartId, preparation!); throw; }
        return await GetPaymentStateAsync(cartId, access, cancellationToken);
    }

    private static void ValidatePaymentState(PaymentIntentStateRef state, Guid orderId, CheckoutPreparation preparation)
    {
        if (state.PaymentIntentId != preparation.AttemptId || state.OrderId != orderId || state.Amount != preparation.Total
            || !string.Equals(state.Currency, preparation.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment does not match the checkout snapshot.");
    }

    private async Task<Entities.Cart.Cart> LoadAuthorizedAsync(Guid cartId, CartAccessContext access, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        CartTracking.Detach(_dbContext, tenantId, cartId);
        var cart = await _dbContext.Carts.SingleOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, ct);
        if (cart is null || !CartAccess.IsAuthorized(cart, access)) throw new NotFoundException($"Cart '{cartId}' was not found.");
        return cart;
    }

    private async Task<OrderChargeSummary> LoadChargeAsync(Guid tenantId, Guid orderId, CancellationToken ct)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<OrderChargeSummary>()
                     .Where(entry => entry.Entity.TenantId == tenantId && entry.Entity.OrderId == orderId).ToList())
            entry.State = EntityState.Detached;
        return await _dbContext.OrderChargeSummaries.SingleAsync(s => s.TenantId == tenantId && s.OrderId == orderId, ct);
    }

    private async Task<CheckoutResult> ReplayAsync(Entities.Cart.Cart cart, Guid orderId, CancellationToken ct)
    {
        var summary = await _dbContext.OrderChargeSummaries.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == cart.TenantId && s.OrderId == orderId, ct)
            ?? throw new InvalidOperationException("Checkout snapshot is missing.");
        return new(orderId, summary.InvoiceId, summary.PaymentIntentId, summary.PaymentStatus, summary.Subtotal,
            summary.DiscountTotal, summary.TaxTotal, summary.Total, summary.Currency,
            summary.PaymentClientSecret, summary.PaymentCheckoutUrl, GuestOrderToken(cart));
    }

    private void DetachCheckout(Guid cartId, CheckoutPreparation? preparation, Guid? orderId = null)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        orderId ??= _dbContext.ChangeTracker.Entries<Entities.Cart.Cart>()
            .FirstOrDefault(e => e.Entity.TenantId == tenantId && e.Entity.Id == cartId)?.Entity.OrderId;
        var reservations = _dbContext.ChangeTracker.Entries<Entities.Inventory.InventoryReservation>()
            .Where(e => e.Entity.TenantId == tenantId && e.Entity.HoldRef == cartId).Select(e => e.Entity).ToList();
        var variantIds = preparation?.Stock.Select(line => line.ProductVariantId).ToHashSet() ?? [];
        variantIds.UnionWith(reservations.Where(row => row.ProductVariantId != null).Select(row => row.ProductVariantId!.Value));
        var ingredientIds = reservations.Where(row => row.IngredientId != null).Select(row => row.IngredientId!.Value).ToHashSet();
        var discountId = preparation?.DiscountId ?? _dbContext.ChangeTracker.Entries<OrderChargeSummary>()
            .FirstOrDefault(e => e.Entity.TenantId == tenantId && e.Entity.OrderId == orderId)?.Entity.DiscountId;
        foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry =>
                     entry.Entity is OrderChargeSummary summary && summary.TenantId == tenantId && summary.OrderId == orderId
                     || entry.Entity is OrderDeliveryDetails delivery && delivery.TenantId == tenantId && delivery.OrderId == orderId
                     || entry.Entity is OrderBundleSelection selection && selection.TenantId == tenantId && selection.OrderId == orderId
                     || entry.Entity is Entities.Inventory.InventoryReservation reservation && reservation.TenantId == tenantId && reservation.HoldRef == cartId
                     || entry.Entity is Entities.Inventory.InventoryLevel level && level.TenantId == tenantId
                        && (level.ProductVariantId is { } variantId && variantIds.Contains(variantId)
                            || level.IngredientId is { } ingredientId && ingredientIds.Contains(ingredientId))
                     || entry.Entity is Discount discount && discount.TenantId == tenantId && discount.Id == discountId).ToList())
            entry.State = EntityState.Detached;
        CartTracking.Detach(_dbContext, tenantId, cartId);
    }

    private string? GuestOrderToken(Entities.Cart.Cart cart)
        => cart.BuyerPartyId is null && cart.OrderId is { } orderId ? _guestOrders.Issue(cart.TenantId, orderId) : null;

    public async Task<bool> ConfirmPaymentAsync(Guid orderId, Guid completedPaymentIntentId, decimal amount, string currency,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var existing = await _dbContext.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.OrderId == orderId && c.TenantId == tenantId, cancellationToken);
        if (existing is null) return false;
        var order = await _orders.GetAsync(orderId, cancellationToken) ?? throw new InvalidOperationException("The checkout order was not found.");
        if (order.Status is not (OrderStatusCodes.Draft or "PendingFunding" or OrderStatusCodes.Complete)) return false;
        var preparation = existing.CheckoutPreparationJson is null ? null : CheckoutPreparation.Read(existing);
        bool accepted;
        try
        {
            accepted = await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                DetachCheckout(existing.Id, preparation, orderId);
                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                var cart = await _dbContext.Carts.SingleAsync(c => c.Id == existing.Id && c.TenantId == tenantId, ct);
                var summary = await _dbContext.OrderChargeSummaries.AsNoTracking().SingleOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == orderId, ct);
                if (summary is null) return false;
                summary = await LoadChargeAsync(tenantId, orderId, ct);
                if (summary.PaymentIntentId != completedPaymentIntentId) return false;
                if (cart.CheckoutPreparationJson is not null
                    && CheckoutPreparation.Read(cart).AttemptId != completedPaymentIntentId) return false;
                if (summary.Total != amount || !string.Equals(summary.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Payment amount or currency does not match checkout.");
                if (cart.Status == CartStatuses.CheckedOut && summary.PaymentStatus != CheckoutPaymentStatuses.Captured) return false;
                if (cart.Status != CartStatuses.CheckedOut)
                {
                    if (cart.CheckoutPreparationJson is not null)
                    {
                        var expected = CheckoutPreparation.Read(cart).Stock.GroupBy(line => line.ProductVariantId)
                            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
                        var held = await _dbContext.InventoryReservations.AsNoTracking()
                            .Where(row => row.TenantId == tenantId && row.HoldRef == cart.Id
                                && row.Status == Entities.Inventory.InventoryReservationStatuses.Held)
                            .ToListAsync(ct);
                        var actual = held.Where(row => row.ProductVariantId != null).GroupBy(row => row.ProductVariantId!.Value)
                            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));
                        if (actual.Count != expected.Count || expected.Any(line => actual.GetValueOrDefault(line.Key) != line.Value))
                            throw new InvalidOperationException("Paid checkout stock does not match its recorded hold.");
                    }
                    summary.PaymentStatus = CheckoutPaymentStatuses.Captured;
                    summary.PaymentCheckoutUrl = null;
                    summary.PaymentClientSecret = null;
                    cart.Status = CartStatuses.CheckedOut;
                    CartActivity.ServerEdit(_dbContext, cart, _clock);
                    await _dbContext.SaveChangesAsync(ct);
                    await _inventory.CommitAsync(cart.Id, ct);
                    await _discounts.MarkRedeemedAsync(summary.DiscountId, ct);
                }
                if (transaction is not null) await transaction.CommitAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch
        {
            DetachCheckout(existing.Id, preparation, orderId);
            throw;
        }
        if (!accepted) return false;
        if (order.Status != OrderStatusCodes.Complete)
            await _orders.TransitionAsync(orderId, OrderStatusCodes.Complete, "Payment completed",
                expectedFromStatus: order.Status, cancellationToken: cancellationToken);
        return true;
    }
}
