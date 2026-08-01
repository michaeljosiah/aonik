using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Persistence;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Spec 087 §16 — returns holds nobody claimed, and closes allowance that has lapsed.
///
/// Both sweeps run <b>per tenant</b>, driven by <see cref="FindTenantsWithWorkAsync"/>. They used to
/// read across every tenant and save once, which could never work:
/// <c>AonikDbContextBase.EnforceTenantOnWrites</c> refuses a tenant-scoped write with no ambient
/// tenant, so the first save threw and neither sweep ever completed. Both are idempotent, so a
/// re-run after a partial failure is safe and is the intended recovery.
/// </summary>
internal sealed class UsageSweeper
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public UsageSweeper(SubscriptionsDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    /// <summary>Tenants holding at least one row either sweep would touch.</summary>
    public async Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var fromReservations = await _dbContext.UsageReservations
            .AsNoTracking()
            .AcrossTenants()
            .Where(r => !r.IsDeleted && r.Status == UsageReservationStatuses.Held && r.ExpiresAt <= now)
            .Select(r => r.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromGrants = await _dbContext.EntitlementGrants
            .AsNoTracking()
            .AcrossTenants()
            .Where(g => !g.IsDeleted && g.Status == GrantStatuses.Open && g.ExpiresAt != null && g.ExpiresAt <= now)
            .Select(g => g.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromReservations.Concat(fromGrants).Distinct().ToList();
    }

    /// <summary>
    /// Expires held reservations past their deadline and returns each hold to the exact grants its
    /// allocations name.
    /// </summary>
    /// <remarks>
    /// Without this a crashed dispatch strands allowance forever: the units are neither consumed
    /// nor available, so a subscriber slowly loses what they paid for with no way to notice why.
    /// </remarks>
    public async Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var stale = await _dbContext.UsageReservations
            .Where(r => r.TenantId == tenantId && r.Status == UsageReservationStatuses.Held && r.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return 0;

        var reservationIds = stale.Select(r => r.Id).ToList();

        var allocations = await _dbContext.UsageReservationAllocations
            .AcrossTenants()
            .Where(a => reservationIds.Contains(a.ReservationId))
            .ToListAsync(cancellationToken);

        var grantIds = allocations.Select(a => a.GrantId).Distinct().ToList();

        var grants = await _dbContext.EntitlementGrants
            .AcrossTenants()
            .Where(g => grantIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        foreach (var allocation in allocations)
        {
            if (grants.TryGetValue(allocation.GrantId, out var grant))
            {
                // Returned to the grant it came from, not spread evenly — otherwise a hold taken
                // against expiring allowance could come back as permanent units, or vice versa.
                grant.Held = Math.Max(0, grant.Held - allocation.Quantity);
            }
        }

        foreach (var reservation in stale)
            reservation.Status = UsageReservationStatuses.Expired;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    /// <summary>
    /// Closes grants past their expiry, stamping <c>ClosedAt</c>.
    /// </summary>
    /// <remarks>
    /// Draw-down already ignores an expired grant on read, so this changes no subscriber's
    /// allowance. It exists so <b>breakage is a recorded event</b> rather than something inferred
    /// by re-running a date comparison months later — "how much lapsed unused, and when" is an
    /// accounting question, and the answer should be written down when it happens.
    /// </remarks>
    public async Task<int> CloseExpiredGrantsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var expired = await _dbContext.EntitlementGrants
            .Where(g => g.TenantId == tenantId && g.Status == GrantStatuses.Open && g.ExpiresAt != null && g.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return 0;

        foreach (var grant in expired)
        {
            grant.Status = GrantStatuses.Closed;
            grant.ClosedAt = now;

            // A grant still holding units when it expires means a reservation outlived it. The
            // hold is released by the reservation sweep; zeroing it here would double-return.
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
