using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Persistence;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Usage;

/// <summary>
/// Spec 087 §16 — returns holds nobody claimed, and closes allowance that has lapsed.
///
/// Both sweeps run <b>across every tenant</b>: a hold left behind by a crashed dispatch belongs to
/// whichever tenant took it, and no request context exists to scope by. Both are idempotent, so a
/// re-run after a partial failure is safe and is the intended recovery.
/// </summary>
internal sealed class UsageSweeper
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly IClock _clock;

    public UsageSweeper(SubscriptionsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
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

        var stale = await _dbContext.UsageReservations
            .AcrossTenants()
            .Where(r => r.Status == UsageReservationStatuses.Held && r.ExpiresAt <= now)
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

        var expired = await _dbContext.EntitlementGrants
            .AcrossTenants()
            .Where(g => g.Status == GrantStatuses.Open && g.ExpiresAt != null && g.ExpiresAt <= now)
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
