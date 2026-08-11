using System.Text.Json;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Purchases;

/// <summary>
/// Spec 087 §12.4 — buying units of a meter outright.
///
/// Pricing is resolved <b>server-side</b> from the meter's current offer. There is deliberately no
/// caller-supplied price anywhere on this path: accepting one would let units be bought at any
/// amount, on a path that moves real money.
/// </summary>
internal sealed class EntitlementPurchaseService : IEntitlementPurchaseService
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly SubscriberAuthorization _authorization;
    private readonly IOrderService _orders;
    private readonly IInvoiceWriter _invoices;
    private readonly IClock _clock;

    public EntitlementPurchaseService(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        SubscriberAuthorization authorization,
        IOrderService orders,
        IInvoiceWriter invoices,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _authorization = authorization;
        _orders = orders;
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<EntitlementPurchaseRef> CreateAsync(
        SubscriberRef subscriber,
        string meterCode,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageBillingForAsync(subscriber, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var code = meterCode.Trim().ToLowerInvariant();

        var offer = await CurrentOfferAsync(tenantId, code, cancellationToken)
            ?? throw new NotFoundException($"No active offer exists for meter '{meterCode}'.");

        if (quantity < offer.MinQuantity)
            throw new InvalidStateException($"Minimum purchasable quantity for '{code}' is {offer.MinQuantity}.");

        if (offer.MaxQuantity is { } max && quantity > max)
            throw new InvalidStateException($"Maximum purchasable quantity for '{code}' is {max}.");

        var total = quantity * offer.UnitPrice;

        // The offer VERSION rides on the line, so a later price change cannot restate a completed
        // purchase, and the grant can be materialised at the price actually charged.
        var provenance = JsonSerializer.Serialize(new
        {
            meterCode = code,
            offerId = offer.Id,
            offerVersion = offer.Version,
            subscriberKind = subscriber.Kind,
            subscriberId = subscriber.Id
        });

        var order = await _orders.CreateAsync(
            new CreateOrderCommand(
                OrderTypeCodes.EntitlementPurchase,
                PayerPartyId: subscriber.Kind == SubscriberKinds.Party ? subscriber.Id : null,
                CurrencyIn: offer.Currency,
                Items:
                [
                    new OrderItemCommand(
                        ItemType: "EntitlementUnits",
                        ItemIndex: 0,
                        AmountIn: total,
                        CurrencyIn: offer.Currency,
                        Quantity: quantity,
                        UnitPrice: offer.UnitPrice,
                        Sku: code,
                        DetailsJson: provenance)
                ],
                IdempotencyKey: $"entitlement:{subscriber.Kind}:{subscriber.Id}:{code}:{_clock.UtcNow:yyyyMMddHHmmssfff}",
                ProvenanceJson: provenance),
            cancellationToken);

        await _invoices.CreateForOrderAsync(
            new CreateInvoiceForOrderCommand(
                OrderId: order.Id,
                CustomerId: subscriber.Id,
                Currency: offer.Currency,
                Lines:
                [
                    // The description is human-facing; the METER is carried in metadata, which is
                    // what the settlement resolver reads (Spec 087 O8).
                    new InvoiceLineSpec($"{quantity} x {code}", quantity, offer.UnitPrice)
                ],
                IdempotencyKey: $"entitlement-invoice:{order.Id}"),
            cancellationToken);

        return new EntitlementPurchaseRef(order.Id, code, quantity, total, offer.Currency);
    }

    /// <summary>
    /// Materialises the purchased grant once the order is paid for. Idempotent on the order, so an
    /// at-least-once payment event cannot grant the units twice.
    /// </summary>
    public async Task<Guid?> MaterialiseOnSettlementAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var order = await _orders.GetAsync(orderId, cancellationToken);
        if (order is null || !string.Equals(order.OrderType, OrderTypeCodes.EntitlementPurchase, StringComparison.OrdinalIgnoreCase))
            return null;

        var existing = await _dbContext.EntitlementGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.SourceOrderId == orderId, cancellationToken);

        if (existing is not null)
            return existing.Id;

        var line = order.Items.FirstOrDefault();
        if (line?.Sku is null || line.Quantity is not { } quantity || line.UnitPrice is not { } unitPrice)
            return null;

        var subscriber = ReadSubscriber(line.DetailsJson);
        if (subscriber is null)
            return null;

        var grant = new EntitlementGrant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            MeterCode = line.Sku,
            Source = GrantSources.Purchase,
            Allowance = quantity,
            Consumed = 0,
            Held = 0,
            // Purchased units never expire — that asymmetry with plan allowance is the whole point
            // of the two sources, and it is why draw-down spends the perishable one first.
            ExpiresAt = null,
            Status = GrantStatuses.Open,
            SourceOrderId = orderId,
            UnitValue = unitPrice,
            UnitValueCurrency = order.CurrencyIn
        };

        _dbContext.EntitlementGrants.Add(grant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return grant.Id;
    }

    private async Task<MeterOffer?> CurrentOfferAsync(Guid tenantId, string meterCode, CancellationToken cancellationToken)
        => await _dbContext.MeterOffers.AsNoTracking()
            .Where(o => o.TenantId == tenantId
                        && o.MeterCode == meterCode
                        && o.Status == MeterOfferStatuses.Published
                        && o.EffectiveFrom <= _clock.UtcNow)
            .OrderByDescending(o => o.Version)
            .FirstOrDefaultAsync(cancellationToken);

    private static SubscriberRef? ReadSubscriber(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(detailsJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("subscriberKind", out var kind) ||
                !root.TryGetProperty("subscriberId", out var id) ||
                !Guid.TryParse(id.GetString(), out var subscriberId))
            {
                return null;
            }

            return new SubscriberRef(kind.GetString() ?? string.Empty, subscriberId);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
