using System.Text.Json;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Spec 087 §9, §10 — enforcement of counter allowances.
///
/// P3 covers the counter path: reserve, commit, release. Ceilings and flags land in P4, and both
/// throw here rather than silently succeeding — a meter kind this cannot enforce must not look
/// like one it can.
/// </summary>
internal sealed class UsageMeter : IUsageMeter
{
    private static readonly TimeSpan DefaultHold = TimeSpan.FromMinutes(15);

    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly SubscriberAuthorization _authorization;
    private readonly IClock _clock;

    public UsageMeter(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        SubscriberAuthorization authorization,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<UsageReservationRef> ReserveAsync(
        SubscriberRef subscriber,
        string meterCode,
        decimal quantity,
        string idempotencyKey,
        TimeSpan? holdFor = null,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanActForAsync(subscriber, cancellationToken);

        if (quantity <= 0)
            throw new InvalidStateException("Reserved quantity must be positive.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidStateException("An idempotency key is required.");

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var existing = await _dbContext.UsageReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
            return new UsageReservationRef(existing.Id, existing.MeterCode, existing.Quantity, existing.ExpiresAt);

        var grants = await OpenGrantsInDrawDownOrderAsync(tenantId, subscriber, meterCode, cancellationToken);

        var available = grants.Sum(g => g.Allowance - g.Consumed - g.Held);
        if (available < quantity)
            throw new EntitlementExceededException(meterCode, quantity, available);

        var reservation = new UsageReservation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            MeterCode = meterCode,
            Quantity = quantity,
            Status = UsageReservationStatuses.Held,
            ExpiresAt = _clock.UtcNow.Add(holdFor ?? DefaultHold),
            IdempotencyKey = idempotencyKey
        };

        _dbContext.UsageReservations.Add(reservation);

        // Hold against SPECIFIC grants, in draw-down order. Two things depend on this: bumping
        // each grant's RowVersion is what makes the concurrency check engage at all, and the
        // per-grant rows are what let a release or a short commit return units to exactly the
        // grants they came from.
        var remaining = quantity;
        var ordinal = 0;

        foreach (var grant in grants)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, grant.Allowance - grant.Consumed - grant.Held);
            if (take <= 0)
                continue;

            grant.Held += take;
            remaining -= take;

            _dbContext.UsageReservationAllocations.Add(new UsageReservationAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReservationId = reservation.Id,
                GrantId = grant.Id,
                Quantity = take,
                Ordinal = ordinal++
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UsageReservationRef(reservation.Id, meterCode, quantity, reservation.ExpiresAt);
    }

    public async Task<UsageCommitResult> CommitAsync(
        Guid reservationId,
        decimal actualQuantity,
        UsageSource source,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var reservation = await _dbContext.UsageReservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Usage reservation '{reservationId}' was not found.");

        await _authorization.EnsureCanActForAsync(
            new SubscriberRef(reservation.SubscriberKind, reservation.SubscriberId), cancellationToken);

        if (reservation.Status != UsageReservationStatuses.Held)
            throw new InvalidStateException($"Reservation is '{reservation.Status}' and cannot be committed.");

        if (reservation.ExpiresAt <= _clock.UtcNow)
            throw new InvalidStateException("Reservation has expired; its hold has been returned.");

        if (actualQuantity < 0)
            throw new InvalidStateException("Committed quantity cannot be negative.");

        if (actualQuantity > reservation.Quantity)
        {
            // Not silently topped up: the extra was never held, so nothing guarantees it is
            // available. A caller needing more should reserve more.
            throw new InvalidStateException(
                $"Committed quantity {actualQuantity} exceeds the reserved {reservation.Quantity}.");
        }

        var allocations = await _dbContext.UsageReservationAllocations
            .Where(a => a.ReservationId == reservation.Id)
            .OrderBy(a => a.Ordinal)
            .ToListAsync(cancellationToken);

        var grantIds = allocations.Select(a => a.GrantId).ToList();
        var grants = await _dbContext.EntitlementGrants
            .Where(g => grantIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        // Trim from the TAIL of the draw-down order, never pro rata. Pro-rata trimming would
        // consume some purchased units while returning some expiring ones — burning permanent
        // value and handing back allowance that then lapses worthless.
        var toConsume = actualQuantity;
        var committed = new List<GrantAllocation>();

        foreach (var allocation in allocations)
        {
            var grant = grants[allocation.GrantId];
            var consumeHere = Math.Min(allocation.Quantity, Math.Max(0, toConsume));

            grant.Held -= allocation.Quantity;
            grant.Consumed += consumeHere;
            toConsume -= consumeHere;

            if (consumeHere > 0)
                committed.Add(new GrantAllocation(grant.Id, grant.Source, consumeHere, grant.ExpiresAt));
        }

        reservation.Status = UsageReservationStatuses.Committed;

        var record = new UsageRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = reservation.SubscriberKind,
            SubscriberId = reservation.SubscriberId,
            SubscriptionId = reservation.SubscriptionId,
            MeterCode = reservation.MeterCode,
            Quantity = actualQuantity,
            AllocationsJson = JsonSerializer.Serialize(committed),
            OccurredAt = _clock.UtcNow,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            ProviderCost = source.ProviderCost,
            ProviderCostCurrency = source.ProviderCostCurrency
        };

        _dbContext.UsageRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UsageCommitResult(record.Id, actualQuantity, committed);
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var reservation = await _dbContext.UsageReservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TenantId == tenantId, cancellationToken);

        // Idempotent: releasing an already-released, expired or committed hold is a no-op. The
        // caller's intent — do not charge for this — is satisfied either way.
        if (reservation is null || reservation.Status != UsageReservationStatuses.Held)
            return;

        await ReturnHoldAsync(reservation, UsageReservationStatuses.Released, cancellationToken);
    }

    public Task ClaimSlotAsync(SubscriberRef subscriber, string meterCode, string holderRef, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Ceiling meters arrive in Spec 087 P4. Throwing rather than succeeding: a meter kind "
            + "this cannot yet enforce must not look like one it can.");

    public Task ReleaseSlotAsync(SubscriberRef subscriber, string meterCode, string holderRef, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Ceiling meters arrive in Spec 087 P4.");

    public Task<bool> HasFlagAsync(SubscriberRef subscriber, string meterCode, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Flag meters arrive in Spec 087 P4. Returning false would be worse than throwing — a "
            + "caller cannot tell 'not entitled' from 'not implemented'.");

    // ---- internals ---------------------------------------------------------------------------

    private async Task ReturnHoldAsync(
        UsageReservation reservation,
        string status,
        CancellationToken cancellationToken)
    {
        var allocations = await _dbContext.UsageReservationAllocations
            .Where(a => a.ReservationId == reservation.Id)
            .ToListAsync(cancellationToken);

        var grantIds = allocations.Select(a => a.GrantId).ToList();
        var grants = await _dbContext.EntitlementGrants
            .Where(g => grantIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        foreach (var allocation in allocations)
            grants[allocation.GrantId].Held -= allocation.Quantity;

        reservation.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Open grants in draw-down order: perishable first (soonest expiry), never-expiring last, ties
    /// oldest-first. Spending the permanent units before the expiring ones would destroy value the
    /// subscriber paid cash for.
    /// </summary>
    private async Task<List<EntitlementGrant>> OpenGrantsInDrawDownOrderAsync(
        Guid tenantId,
        SubscriberRef subscriber,
        string meterCode,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var grants = await _dbContext.EntitlementGrants
            .Where(g => g.TenantId == tenantId
                        && g.SubscriberKind == subscriber.Kind
                        && g.SubscriberId == subscriber.Id
                        && g.MeterCode == meterCode
                        && g.Status == GrantStatuses.Open
                        && (g.ExpiresAt == null || g.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        return grants
            .OrderBy(g => g.ExpiresAt.HasValue ? 0 : 1)
            .ThenBy(g => g.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(g => g.CreatedAt)
            .ToList();
    }
}
