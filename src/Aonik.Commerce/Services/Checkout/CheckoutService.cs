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
using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Settings;
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
    private readonly ITenantSettingStore _settings;
    private readonly DeliveryReservationService _deliveryReservations;
    private readonly CheckoutLoyaltyQuotes? _loyaltyQuotes;
    private readonly GiftCardPurchasePricing? _giftPricing;
    private readonly CheckoutGiftCards? _giftCards;

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
        IDeliveryCoverageService coverage, IPartyService parties, IClock clock, ITenantSettingStore settings,
        CheckoutLoyaltyQuotes? loyaltyQuotes = null, GiftCardPurchasePricing? giftPricing = null,
        CheckoutGiftCards? giftCards = null)
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
        _settings = settings;
        _loyaltyQuotes = loyaltyQuotes;
        _giftPricing = giftPricing;
        _giftCards = giftCards;
        _deliveryReservations = new DeliveryReservationService(dbContext, tenantProvider, clock);
    }

    private static readonly JsonSerializerOptions EnvelopeSerializerOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>The ItemType of a materialised delivery charge (Spec 068 §9) — excluded from
    /// goods-discount apportionment in reporting.</summary>
    internal const string DeliveryFeeItemType = "DeliveryFee";
    internal const string GreetingCardItemType = "GreetingCard";

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
        var gift = draft?.Gift is { GiftIntent: true } selectedGift ? selectedGift : null;
        if (gift is not null && cart.BoxBundleProductId is null)
            throw new StorefrontValidationException("Gift fulfilment requires a food box.");
        var requestedDelivery = command.Delivery ?? (cart.BoxBundleProductId != null || draft?.Address != null
            || draft?.DeliveryDate != null ? CartDraftData.Delivery(draft) : null);
        var acceptedTerms = cart.BoxBundleProductId is not null
            ? await SaleTermsPolicy.AcceptAsync(_settings, tenantId, draft?.AcceptedTermsVersion, _clock.UtcNow, cancellationToken)
            : null;

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
            if (gift is not null && details.Recipient is null)
                throw new StorefrontValidationException("Recipient: enter the gift recipient's name and phone number.");
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
                    $"{details.Purchaser.FirstName} {details.Purchaser.LastName}", details.Purchaser.Phone), details.Notes,
                gift is null ? null : new OrderGiftDto(gift.HidePrices, gift.IncludeGreetingCard,
                    gift.IncludeGreetingCard ? gift.GreetingCardMessage : null), acceptedTerms);
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
        var subtotal = box is not null ? box.GoodsTotal + box.AddOnGoodsTotal + box.GreetingCardCharged
            : cart.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity);
        if (box is not null && subtotal <= 0)
        {
            // R4 — a delivery charge must not carry a nonpositive goods figure over the final
            // total guard and mint a negative-priced retail item.
            throw new StorefrontValidationException(
                "The goods total for this box is zero or below; it cannot be checked out.");
        }
        // Freeze the authoritative stock and order inputs before claiming checkout.
        var reservationLines = new List<InventoryReservationLine>();
        foreach (var item in cart.Items)
        {
            if (item.LineKind == CartLineKinds.GiftCardValue) continue;
            if (_giftPricing != null) await _giftPricing.RejectOrdinaryVariantAsync(item.ProductVariantId, cancellationToken);
            if (item.IsBundle)
            {
                foreach (var sel in item.Selections)
                {
                    if (_giftPricing != null) await _giftPricing.RejectOrdinaryVariantAsync(sel.ProductVariantId, cancellationToken);
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
                if (item.LineKind == CartLineKinds.GiftCardValue) continue;
                orderItems.Add(new OrderItemCommand(
                    ItemType: OrderTypeCodes.ProductPurchase,
                    ItemIndex: index,
                    AmountIn: item.UnitPriceSnapshot * item.Quantity,
                    CurrencyIn: cart.Currency,
                    Quantity: item.Quantity,
                    UnitPrice: item.UnitPriceSnapshot,
                    ProductId: item.IsBundle ? item.BundleProductId : item.ProductVariantId,
                    Sku: item.Sku,
                    NameSnapshot: item.NameSnapshot));
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
                DetailsJson: box.EnvelopeJson,
                NameSnapshot: $"{box.Size}-dish box"));
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
                    DetailsJson: priced is null ? null : JsonSerializer.Serialize(priced, EnvelopeSerializerOptions),
                    NameSnapshot: line.NameSnapshot));
            }

            if (box.GreetingCardCharged > 0)
                orderItems.Add(GreetingCardPricing.Item(nextIndex++, box.GreetingCardCharged, cart.Currency));

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
                    DetailsJson: null,
                    NameSnapshot: "Delivery"));
            }
        }

        var giftPurchase = _giftPricing == null ? null : await _giftPricing.AppendAsync(cart, orderItems, cancellationToken);
        if (giftPurchase == null && cart.GiftCardPurchaseJson != null)
            throw new StorefrontValidationException("Gift-card purchasing is not available.");
        subtotal = orderItems.Where(x => x.ItemType != DeliveryFeeItemType).Sum(x => x.AmountIn);
        var giftValue = giftPurchase?.Checkout.Purchase?.FaceValue ?? 0m;
        var code = command.DiscountCode ?? draft?.DiscountCode;
        var discountLines = string.IsNullOrWhiteSpace(code) ? Array.Empty<DiscountChargeLine>()
            : await CheckoutDiscountLines.FromOrderItemsAsync(_dbContext, tenantId, orderItems, cancellationToken);
        var discount = await _discounts.ComputeAsync(code, discountLines, cart.Currency, cancellationToken);
        var taxable = subtotal - discount.Amount;
        var taxBeforePoints = await _tax.CalculateAsync(taxable - giftValue, cart.Currency, cancellationToken);
        LoyaltyCheckout? loyalty = null;
        if (_loyaltyQuotes is not null)
        {
            var quote = await _loyaltyQuotes.CalculateAsync(cart, orderItems, discount,
                taxable + taxBeforePoints + (box?.DeliveryCharged ?? 0m), cancellationToken);
            if (quote.Quote?.ReasonCode is not null)
                throw new StorefrontValidationException(quote.Quote.Message ?? "The selected points cannot be applied. Reload the quote.");
            loyalty = quote.Checkout;
        }
        else if (draft?.RequestedPoints > 0)
            throw new StorefrontValidationException("Loyalty points are not available for this checkout.");
        var pointsValue = loyalty?.PointsAppliedValue ?? 0m;
        var tax = pointsValue > 0m
            ? await _tax.CalculateAsync(taxable - pointsValue - giftValue, cart.Currency, cancellationToken)
            : taxBeforePoints;
        var total = taxable - pointsValue + tax + (box?.DeliveryCharged ?? 0m);
        var giftFunding = _giftCards == null ? null : await _giftCards.QuoteAsync(cart, orderItems, discount, loyalty,
            total, tax, cancellationToken);
        if (cart.GiftCardTenderJson != null && (giftFunding?.Checkout?.Tender == null || giftFunding.ReasonCode != null))
            throw new StorefrontValidationException("The gift-card balance changed or is unavailable. Review the payment quote.");
        var giftCard = giftPurchase?.Checkout ?? giftFunding?.Checkout;
        var cardAmount = giftFunding?.CardAmount ?? total;
        if (giftFunding != null && (command.ExpectedTotal == null || command.ExpectedCardAmount != cardAmount))
            throw new StorefrontValidationException("Accept the current total and exact card remainder before paying.");
        if (giftPurchase != null && command.ExpectedTotal == null)
            throw new StorefrontValidationException("Accept the current gift-card total before paying.");
        if (loyalty != null && giftFunding?.Checkout?.Tender is { } giftTender)
            loyalty = LoyaltyCheckoutCalculator.ApplyGiftFunding(loyalty, giftTender.Lines);
        if ((command.ExpectedTotal is { } expectedTotal && expectedTotal != total)
            || ((!string.IsNullOrWhiteSpace(code) || gift is { IncludeGreetingCard: true } || pointsValue > 0m) && command.ExpectedTotal is null))
            throw new DiscountException(DiscountException.PriceChanged);
        if (total <= 0)
            throw new StorefrontValidationException("The payable total for this cart is zero or below; it cannot be checked out.");
        if (!string.Equals(command.Provider, "Stripe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(command.PaymentMethodType, "Card", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(cart.Currency, "GBP", StringComparison.OrdinalIgnoreCase)
            || (cardAmount > 0m && cardAmount < 0.30m) || total > 999999.99m || decimal.Truncate(total * 100m) != total * 100m)
            throw new StorefrontValidationException("Checkout supports Stripe card payments in GBP, in whole pennies from 0.30 to 999999.99.");

        var purchaser = delivery?.Purchaser ?? (draft?.Purchaser is { } contact ? CheckoutContactValidation.ValidateContact(contact) : null);
        if (cart.BuyerPartyId is null && purchaser is null)
            throw new StorefrontValidationException("Purchaser contact details are required for guest payment.");
        if (cart.CheckoutState == CartCheckoutStates.Retryable && command.CustomerAccountId is not null)
            throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");

        var invoiceLines = orderItems.Select(i => new InvoiceLineSpec(i.NameSnapshot ?? i.ItemType,
            i.Quantity ?? 1m, i.UnitPrice ?? i.AmountIn)).ToList();
        if (discount.Amount > 0) invoiceLines.Add(new($"Discount ({discount.Code})", 1m, -discount.Amount));
        if (pointsValue > 0m) invoiceLines.Add(new("Loyalty points", 1m, -pointsValue));
        if (tax > 0) invoiceLines.Add(new("Tax", 1m, tax));
        var selections = new List<CheckoutSelection>();
        var signatureFlags = await CheckoutDisplayFacts.ReadSignaturesAsync(_dbContext, _settings, tenantId,
            cart.Items.SelectMany(item => item.IsBundle
                ? item.Selections.Select(selection => selection.ProductVariantId)
                : new[] { item.ProductVariantId }).Distinct().ToList(), cancellationToken);
        foreach (var (lineIndex, item) in bundleLineIndices)
            selections.AddRange(item.Selections.Select(s => new CheckoutSelection(lineIndex, s.BundleSlotId,
                s.ProductVariantId, s.Quantity * item.Quantity, s.Sku,
                NameSnapshot: s.NameSnapshot, IsSignatureSnapshot: signatureFlags.GetValueOrDefault(s.ProductVariantId))));
        if (box is not null)
            selections.AddRange(box.Lines.Select(pair => new CheckoutSelection(0, pair.Line.BoxBundleSlotId!.Value,
                pair.Line.ProductVariantId, pair.Line.Quantity, pair.Line.Sku, pair.Priced.CanonicalSelectionJson,
                BoxCartService.TruncateSummary(pair.Priced.Summary), pair.Priced.Adjustment,
                pair.Priced.UnitSurcharge ?? 0m, JsonSerializer.Serialize(pair.Priced, EnvelopeSerializerOptions),
                pair.Line.NameSnapshot, signatureFlags.GetValueOrDefault(pair.Line.ProductVariantId))));
        Guid? guestPartyId = cart.BuyerPartyId is null
            ? Guid.NewGuid()
            : null;
        if (loyalty is not null)
            loyalty = loyalty with { PartyId = cart.BuyerPartyId ?? guestPartyId!.Value,
                IsGuest = cart.BuyerPartyId is null, PayableTotal = total };
        var preparation = new CheckoutPreparation(Guid.NewGuid(), guestPartyId, cart.Currency,
            command.Provider, command.PaymentMethodType, command.ReturnUrl, command.CancelUrl, command.CustomerAccountId,
            subtotal, discount.Amount, discount.DiscountId, discount.Code, tax, total, orderItems, invoiceLines,
            reservationLines.Select(line => new CheckoutStockLine(line.Item.Id, line.Quantity)).ToList(), selections, delivery,
            DiscountAllocations: discount.Allocations, GreetingCardCharged: box?.GreetingCardCharged ?? 0m, Loyalty: loyalty,
            GiftCard: giftCard, GiftCardDelivery: giftPurchase?.Delivery, Purchaser: purchaser);
        _ = preparation.Serialize();
        try { preparation = await ClaimPreparationAsync(cart.Id, preparation, access, command.RequireFreshCart, cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            DetachCheckout(cart.Id, preparation);
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
            DetachCheckout(cart.Id, preparation);
            throw;
        }
        return await ResumeAsync(cart.Id, preparation, access, cancellationToken);
    }

    private async Task<CheckoutPreparation> ClaimPreparationAsync(Guid cartId, CheckoutPreparation preparation,
        CartAccessContext access, bool requireFreshCart, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
                {
                    DetachCheckout(cartId, preparation);
                    // Native versions on cart, hold and pool arbitrate the single atomic save.
                    // Retaining read locks here would block competing edits before that claim.
                    await using var transaction = _dbContext.Database.IsRelational()
                        ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
                    var current = await LoadAuthorizedAsync(cartId, access, ct);
                    if (!requireFreshCart && current.CheckoutPreparationJson is not null
                        && (current.CheckoutState is CartCheckoutStates.Preparing or CartCheckoutStates.AwaitingPayment
                            || current.Status == CartStatuses.CheckedOut))
                        return CheckoutPreparation.Read(current);
                    CartWriteGuard.RequireCurrent(current, access);
                    var claimed = preparation with
                    {
                        CreateAccount = current.BuyerPartyId is null && CartDraftData.Read(current)?.CreateAccount == true
                    };
                    if (preparation.DiscountId is not null)
                    {
                        var lines = await CheckoutDiscountLines.FromOrderItemsAsync(_dbContext, current.TenantId,
                            preparation.Items, ct);
                        var reservationId = await _discounts.ReserveTrackedAsync(cartId, preparation.AttemptId,
                            preparation.DiscountCode, lines, preparation.Currency,
                            new DiscountComputation(preparation.DiscountId, preparation.DiscountCode,
                                preparation.DiscountTotal, preparation.DiscountAllocations ?? []), ct);
                        claimed = claimed with { DiscountReservationId = reservationId };
                    }
                    if (current.BoxBundleProductId is not null)
                    {
                        var hold = await _deliveryReservations.BeginPaymentTrackedAsync(current,
                            preparation.Delivery!.DeliveryDate, preparation.AttemptId, ct);
                        claimed = claimed with
                        {
                            DeliveryReservationId = hold.Id, ProviderStartDeadlineUtc = hold.PaymentDeadlineUtc
                        };
                    }
                    var json = claimed.Serialize();
                    current.CheckoutPreparationJson = json;
                    current.CheckoutState = CartCheckoutStates.Preparing;
                    CartActivity.ServerEdit(_dbContext, current, _clock);
                    await _dbContext.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return claimed;
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                DetachCheckout(cartId, preparation);
                var current = await LoadAuthorizedAsync(cartId, access, cancellationToken);
                if (!CartWriteGuard.IsEditable(current) || !ActiveBoxCarts.MatchesVersion(current, access.ExpectedCartVersion))
                    throw; // The caller converges a competing checkout; only shared pool contention retries here.
            }
        }
    }

    private async Task<CheckoutResult> ResumeAsync(Guid cartId, CheckoutPreparation preparation,
        CartAccessContext access, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (preparation.GuestPartyId is { } guestId)
        {
            var contact = preparation.Purchaser ?? preparation.Delivery?.Purchaser
                ?? throw new InvalidOperationException("Guest purchaser details are missing.");
            await _parties.EnsureUnverifiedGuestPartyAsync(guestId, cartId,
                new CreatePartyRequest($"{contact.FirstName} {contact.LastName}", "Person", contact.FirstName,
                    contact.LastName, contact.Phone, contact.Email, preparation.Delivery?.Address.CountryCode), ct);
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
                order = await _orders.RefreshPendingItemsAsync(order.Id, preparation.AttemptId, cart.BuyerPartyId ?? preparation.GuestPartyId, preparation.Currency, preparation.Items, token);
                cart.OrderId = order.Id;
                if (preparation.DeliveryReservationId is { } reservationId)
                    await _deliveryReservations.BindOrderTrackedAsync(cart, reservationId, preparation.AttemptId,
                        order.Id, preparation.Delivery!.DeliveryDate, token);
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
                summary.GreetingCardCharged = preparation.GreetingCardCharged;
                summary.DiscountTotal = preparation.DiscountTotal; summary.DiscountId = preparation.DiscountId;
                summary.PointsAppliedValue = preparation.Loyalty?.PointsAppliedValue ?? 0m;
                summary.LoyaltyJson = CheckoutLoyaltyData.Serialize(preparation.Loyalty is null ? null
                    : preparation.Loyalty with { Lines = preparation.Loyalty.Lines.Select(line => line with
                        { OrderItemId = order.Items.Single(item => item.ItemIndex == line.ItemIndex).Id }).ToList() });
                summary.DiscountCode = preparation.DiscountCode; summary.TaxTotal = preparation.TaxTotal; summary.Total = preparation.Total;
                var mappedGift = preparation.GiftCard;
                if (mappedGift != null)
                {
                    mappedGift = mappedGift with
                    {
                        Purchase = mappedGift.Purchase is { } purchase ? purchase with
                            { OrderItemId = order.Items.Single(x => x.ItemIndex == purchase.ItemIndex).Id } : null,
                        Tender = mappedGift.Tender is { } tender ? tender with
                            { Lines = tender.Lines.Select(line => line with
                                { OrderItemId = order.Items.Single(x => x.ItemIndex == line.ItemIndex).Id }).ToList() } : null
                    };
                }
                summary.GiftCardJson = CheckoutGiftCards.Serialize(mappedGift);
                if (preparation.GiftCardDelivery is { } giftDelivery)
                    await GiftCardDeliveryData.StageTrackedAsync(_dbContext, tenantId, cartId, order.Id,
                        preparation.AttemptId, giftDelivery with
                        { OrderItemId = order.Items.Single(x => x.ItemIndex == giftDelivery.ItemIndex).Id }, token);
                summary.DiscountAllocationsJson = preparation.DiscountAllocations is null ? null
                    : DiscountAllocationSnapshot.Serialize(preparation.DiscountAllocations.Select(allocation =>
                        new OrderDiscountAllocation(order.Items.Single(item => item.ItemIndex == allocation.ItemIndex).Id,
                            allocation.Amount)).ToList());
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
                        UnitSurcharge = selection.UnitSurcharge, PersonalisationEnvelopeJson = selection.PersonalisationEnvelopeJson,
                        NameSnapshot = selection.NameSnapshot, IsSignatureSnapshot = selection.IsSignatureSnapshot
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
        var paymentSummary = await LoadChargeAsync(tenantId, orderId, ct);
        var paymentLoyalty = CheckoutLoyaltyData.Read(paymentSummary.LoyaltyJson);
        var paymentGiftCard = CheckoutGiftCards.Read(paymentSummary.GiftCardJson);
        var recordedState = await _payments.GetStateAsync(preparation.AttemptId, ct);
        if (recordedState is not null) ValidatePaymentState(recordedState, orderId, preparation);
        var intent = recordedState is not null && (recordedState.CheckoutUrl is not null || recordedState.CanNoLongerPay
                || recordedState.Status == CheckoutPaymentStatuses.Captured)
            ? new PaymentIntentRef(recordedState.PaymentIntentId, recordedState.Status, CheckoutUrl: recordedState.CheckoutUrl)
            : await _payments.CreateGuestIntentForOrderAsync(new CreateGuestPaymentIntentForOrderCommand(orderId,
                preparation.Total, preparation.Currency, preparation.Provider, preparation.PaymentMethodType,
                preparation.ReturnUrl, preparation.CancelUrl, preparation.AttemptId, $"commerce:{cartId:N}:{preparation.AttemptId:N}",
                preparation.ProviderStartDeadlineUtc, Loyalty: paymentLoyalty, GiftCard: paymentGiftCard), ct);
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
            charge.PaymentCheckoutUrl = terminal || DeadlinePassed(preparation) ? null : state?.CheckoutUrl ?? intent.CheckoutUrl;
            charge.PaymentClientSecret = terminal || DeadlinePassed(preparation) ? null : intent.ClientSecret ?? charge.PaymentClientSecret;
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
                "RequiresAction" when !DeadlinePassed(preparation) => "requires_action", _ => "processing"
            };
        return new(cart.OrderId, intentId, status, CartWriteGuard.IsEditable(cart), Convert.ToBase64String(cart.RowVersion),
            DeadlinePassed(preparation) || state?.CanNoLongerPay == true || state?.Status == CheckoutPaymentStatuses.Captured ? null : state?.CheckoutUrl);
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
        await RecoverPaymentAsync(cart, preparation!, access, cancellationToken);
        return await GetPaymentStateAsync(cartId, access, cancellationToken);
    }

    private bool DeadlinePassed(CheckoutPreparation? preparation)
        => preparation?.ProviderStartDeadlineUtc is { } deadline && deadline <= _clock.UtcNow;

    public async Task<IReadOnlyList<(Guid ReservationId, Guid TenantId, Guid CartId)>> FindDueDeliveryReservationsAsync(
        Guid? afterReservationId = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = _dbContext.CartDeliveryReservations.AcrossTenants().AsNoTracking()
            .Where(r => !r.IsDeleted && (r.Status == DeliveryReservationStatuses.Held && r.ExpiresAtUtc <= now
                || r.Status == DeliveryReservationStatuses.PaymentPending && r.PaymentDeadlineUtc <= now));
        if (afterReservationId is { } after) due = due.Where(r => r.Id.CompareTo(after) > 0);
        var page = await due.OrderBy(r => r.Id).Take(50)
            .Select(r => new { r.Id, r.TenantId, r.CartId }).ToListAsync(cancellationToken);
        return page.Select(r => (r.Id, r.TenantId, r.CartId)).ToList();
    }

    public async Task ReconcileDeliveryReservationAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken);
        if (cart is null) return;
        var hold = await _dbContext.CartDeliveryReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.CartId == cartId && r.TenantId == tenantId, cancellationToken);
        if (hold is { Status: DeliveryReservationStatuses.PaymentPending }
            && hold.PaymentDeadlineUtc <= _clock.UtcNow && cart.CheckoutPreparationJson is not null)
        {
            var preparation = CheckoutPreparation.Read(cart);
            if (hold.Id != preparation.DeliveryReservationId || hold.PaymentAttemptId != preparation.AttemptId
                || hold.PaymentDeadlineUtc != preparation.ProviderStartDeadlineUtc
                || hold.DeliveryDate != preparation.Delivery?.DeliveryDate
                || hold.OrderId != cart.OrderId
                    && !(cart.CheckoutState == CartCheckoutStates.Preparing && hold.OrderId is null))
                throw new InvalidOperationException("Delivery reservation does not match checkout.");
            await RecoverPaymentAsync(cart, preparation, null, cancellationToken);
        }
        else if (hold is { Status: DeliveryReservationStatuses.Held } && hold.ExpiresAtUtc <= _clock.UtcNow)
        {
            try
            {
                await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
                {
                    DetachCheckout(cartId, null);
                    await using var transaction = _dbContext.Database.IsRelational()
                        ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                    var current = await _dbContext.Carts.SingleAsync(c => c.Id == cartId && c.TenantId == tenantId, ct);
                    if (!CartWriteGuard.IsEditable(current) && current.Status != CartStatuses.Abandoned) return;
                    if (!await _deliveryReservations.ExpireHeldTrackedAsync(current, ct)) return;
                    CartActivity.ServerEdit(_dbContext, current, _clock);
                    await _dbContext.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                }, cancellationToken);
            }
            catch { DetachCheckout(cartId, null); throw; }
        }
    }

    private async Task RecoverPaymentAsync(Entities.Cart.Cart cart, CheckoutPreparation preparation,
        CartAccessContext? access, CancellationToken cancellationToken)
    {
        var cartId = cart.Id;
        var expectedPaymentIntentId = preparation.AttemptId;
        var expectedVersion = Convert.ToBase64String(cart.RowVersion);
        // Invoice creation commits independently, including before Commerce finishes preparation.
        if (preparation.CustomerAccountId is not null || cart.OrderId is { } invoicedOrder
            && await _dbContext.OrderChargeSummaries.AsNoTracking()
                .AnyAsync(s => s.TenantId == cart.TenantId && s.OrderId == invoicedOrder && s.InvoiceId != null, cancellationToken))
            throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");
        // Preparing has never entered Finance. Taking the same parent row claim below prevents
        // a delayed local preparation from crossing into AwaitingPayment after this cancellation.
        PaymentIntentStateRef? state = null;
        if (cart.CheckoutState == CartCheckoutStates.AwaitingPayment)
        {
            state = await _payments.GetStateAsync(expectedPaymentIntentId, cancellationToken);
            if (state is null && DeadlinePassed(preparation))
            {
                var paymentSummary = await LoadChargeAsync(cart.TenantId, cart.OrderId!.Value, cancellationToken);
                // Missing is not cancellation proof. The frozen deadline lets Finance atomically
                // cancel an unstarted attempt even when a delayed creator is racing this sweep.
                await _payments.CreateGuestIntentForOrderAsync(new CreateGuestPaymentIntentForOrderCommand(
                    cart.OrderId!.Value, preparation.Total, preparation.Currency, preparation.Provider,
                    preparation.PaymentMethodType, preparation.ReturnUrl, preparation.CancelUrl,
                    preparation.AttemptId, $"commerce:{cartId:N}:{preparation.AttemptId:N}",
                    preparation.ProviderStartDeadlineUtc,
                    Loyalty: CheckoutLoyaltyData.Read(paymentSummary.LoyaltyJson),
                    GiftCard: CheckoutGiftCards.Read(paymentSummary.GiftCardJson)), cancellationToken);
                state = await _payments.GetStateAsync(expectedPaymentIntentId, cancellationToken);
            }
            if (state is null) return;
            ValidatePaymentState(state, cart.OrderId!.Value, preparation);
            state = await _payments.ExpireAsync(expectedPaymentIntentId, cancellationToken);
            ValidatePaymentState(state, cart.OrderId!.Value, preparation);
            if (state.Status == CheckoutPaymentStatuses.Captured)
            {
                await ConfirmPaymentAsync(state.OrderId, state.PaymentIntentId, state.Amount, state.Currency, cancellationToken);
                return;
            }
            if (!state.CanNoLongerPay) return;
        }
        else if (cart.CheckoutState != CartCheckoutStates.Preparing) return;
        try
        {
            await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                DetachCheckout(cartId, preparation);
                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                var current = access is not null ? await LoadAuthorizedAsync(cartId, access, ct)
                    : await _dbContext.Carts.SingleAsync(c => c.Id == cartId && c.TenantId == cart.TenantId, ct);
                if (CheckoutPreparation.Read(current).AttemptId != expectedPaymentIntentId
                    || current.CheckoutState != cart.CheckoutState || current.Status != CartStatuses.Open)
                    throw new CartWriteConflictException(current, "commerce.cart_conflict", "Checkout changed while payment was resolving.");
                if (!ActiveBoxCarts.MatchesVersion(current, expectedVersion))
                    throw new CartWriteConflictException(current, "commerce.cart_conflict", "The cart changed. Reload checkout.");
                current.CheckoutState = CartCheckoutStates.Retryable;
                if (access is not null) CartActivity.UserEdit(_dbContext, current, _clock);
                else CartActivity.ServerEdit(_dbContext, current, _clock);
                await _dbContext.SaveChangesAsync(ct);
                await _inventory.ReleaseAsync(cartId, ct);
                if (preparation.DeliveryReservationId is not null)
                    await _deliveryReservations.ReleaseTrackedAsync(current, expectedPaymentIntentId, ct);
                if (preparation.DiscountReservationId is { } discountReservationId)
                    await _discounts.ReleaseTrackedAsync(cartId, discountReservationId, expectedPaymentIntentId, ct);
                if (current.OrderId is { } orderId)
                {
                    var summary = await LoadChargeAsync(current.TenantId, orderId, ct);
                    if (summary.PaymentStatus == CheckoutPaymentStatuses.Captured) throw new InvalidOperationException("A paid checkout cannot reopen.");
                    if (summary.InvoiceId is not null) throw new StorefrontValidationException("Invoice checkout recovery requires staff assistance.");
                    summary.PaymentStatus = state?.Status ?? "Cancelled";
                    summary.PaymentCheckoutUrl = null; summary.PaymentClientSecret = null;
                }
                await _dbContext.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
            }, cancellationToken);
        }
        catch { DetachCheckout(cartId, preparation); throw; }
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
        var expired = cart.CheckoutPreparationJson is not null && DeadlinePassed(CheckoutPreparation.Read(cart));
        var giftPaid = CheckoutGiftCards.Read(summary.GiftCardJson)?.Tender?.Amount ?? 0m;
        return new(orderId, summary.InvoiceId, summary.PaymentIntentId, summary.PaymentStatus, summary.Subtotal,
            summary.DiscountTotal, summary.TaxTotal, summary.Total, summary.Currency,
            expired ? null : summary.PaymentClientSecret, expired ? null : summary.PaymentCheckoutUrl, GuestOrderToken(cart),
            CheckoutLoyaltyData.ForOrder(summary, cart),
            giftPaid, summary.Total - giftPaid);
    }

    private void DetachCheckout(Guid cartId, CheckoutPreparation? preparation, Guid? orderId = null)
    {
        _deliveryReservations.Detach(cartId);
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
        _discounts.Detach(cartId, discountId);
        foreach (var entry in _dbContext.ChangeTracker.Entries().Where(entry =>
                     entry.Entity is OrderChargeSummary summary && summary.TenantId == tenantId && summary.OrderId == orderId
                     || entry.Entity is OrderDeliveryDetails delivery && delivery.TenantId == tenantId && delivery.OrderId == orderId
                     || entry.Entity is OrderGiftCardDelivery giftDelivery && giftDelivery.TenantId == tenantId && giftDelivery.CartId == cartId
                     || entry.Entity is OrderBundleSelection selection && selection.TenantId == tenantId && selection.OrderId == orderId
                     || entry.Entity is Entities.Inventory.InventoryReservation reservation && reservation.TenantId == tenantId && reservation.HoldRef == cartId
                     || entry.Entity is Entities.Inventory.InventoryLevel level && level.TenantId == tenantId
                        && (level.ProductVariantId is { } variantId && variantIds.Contains(variantId)
                            || level.IngredientId is { } ingredientId && ingredientIds.Contains(ingredientId))).ToList())
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
                    if (preparation?.DeliveryReservationId is { } reservationId)
                        await _deliveryReservations.CommitTrackedAsync(cart, reservationId, completedPaymentIntentId,
                            orderId, preparation.Delivery!.DeliveryDate, ct);
                    if (preparation?.DiscountReservationId is { } discountReservationId)
                        await _discounts.CommitTrackedAsync(cart.Id, discountReservationId, completedPaymentIntentId,
                            summary.DiscountId ?? throw new InvalidOperationException("The reserved discount is missing."), ct);
                    CartActivity.ServerEdit(_dbContext, cart, _clock);
                    await _dbContext.SaveChangesAsync(ct);
                    await _inventory.CommitAsync(cart.Id, ct);
                    if (preparation?.DiscountReservationId is null)
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
