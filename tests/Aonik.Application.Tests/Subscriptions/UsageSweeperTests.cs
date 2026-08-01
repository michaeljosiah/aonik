using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Usage;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 087 §16 — the two sweeps.
///
/// The reservation sweep is the one that matters to a subscriber: without it a crashed dispatch
/// strands allowance permanently — neither consumed nor available — so they quietly lose what they
/// paid for and nothing explains why.
/// </summary>
public class UsageSweeperTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static (SubscriptionsDbContext Db, TestClock Clock, UsageSweeper Sweeper) Create()
    {
        var clock = new TestClock();
        var db = new SubscriptionsDbContext(
            new DbContextOptionsBuilder<SubscriptionsDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options,
            new TestTenantProvider());

        return (db, clock, new UsageSweeper(db, clock));
    }

    private static EntitlementGrant Grant(decimal allowance, decimal held, DateTime? expiresAt = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        SubscriberKind = SubscriberKinds.Tenant,
        SubscriberId = TenantId,
        MeterCode = "stories",
        Source = GrantSources.Plan,
        Allowance = allowance,
        Consumed = 0,
        Held = held,
        ExpiresAt = expiresAt,
        Status = GrantStatuses.Open
    };

    private static UsageReservation Reservation(DateTime expiresAt, string status = UsageReservationStatuses.Held) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        SubscriberKind = SubscriberKinds.Tenant,
        SubscriberId = TenantId,
        MeterCode = "stories",
        Quantity = 2,
        Status = status,
        ExpiresAt = expiresAt,
        IdempotencyKey = Guid.NewGuid().ToString()
    };

    // ---- reservation sweep --------------------------------------------------------------

    [Fact]
    public async Task ExpireStaleReservationsAsync_Should_ReturnTheHold_ToTheGrantItCameFrom()
    {
        var (db, clock, sweeper) = Create();

        var perishable = Grant(5, held: 2, expiresAt: clock.UtcNow.AddDays(30));
        var permanent = Grant(5, held: 0);
        var reservation = Reservation(clock.UtcNow.AddMinutes(-1));

        db.EntitlementGrants.AddRange(perishable, permanent);
        db.UsageReservations.Add(reservation);
        db.UsageReservationAllocations.Add(new UsageReservationAllocation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            ReservationId = reservation.Id, GrantId = perishable.Id, Quantity = 2, Ordinal = 0
        });
        await db.SaveChangesAsync();

        var swept = await sweeper.ExpireStaleReservationsAsync();

        swept.Should().Be(1);

        // Returned to the grant it came from, not spread evenly — otherwise a hold taken against
        // expiring allowance could come back as permanent units, or the reverse.
        (await db.EntitlementGrants.AsNoTracking().FirstAsync(g => g.Id == perishable.Id)).Held.Should().Be(0);
        (await db.EntitlementGrants.AsNoTracking().FirstAsync(g => g.Id == permanent.Id)).Held.Should().Be(0);
        (await db.UsageReservations.AsNoTracking().FirstAsync()).Status.Should().Be(UsageReservationStatuses.Expired);
    }

    [Fact]
    public async Task ExpireStaleReservationsAsync_Should_LeaveLiveHoldsAlone()
    {
        var (db, clock, sweeper) = Create();

        var grant = Grant(5, held: 2);
        var live = Reservation(clock.UtcNow.AddMinutes(10));

        db.EntitlementGrants.Add(grant);
        db.UsageReservations.Add(live);
        await db.SaveChangesAsync();

        (await sweeper.ExpireStaleReservationsAsync()).Should().Be(0);

        // A dispatch still running must not have its allowance pulled out from under it.
        (await db.EntitlementGrants.AsNoTracking().FirstAsync()).Held.Should().Be(2);
    }

    [Fact]
    public async Task ExpireStaleReservationsAsync_Should_IgnoreAlreadyResolvedReservations()
    {
        var (db, clock, sweeper) = Create();

        db.EntitlementGrants.Add(Grant(5, held: 0));
        db.UsageReservations.Add(Reservation(clock.UtcNow.AddMinutes(-1), UsageReservationStatuses.Committed));
        db.UsageReservations.Add(Reservation(clock.UtcNow.AddMinutes(-1), UsageReservationStatuses.Released));
        await db.SaveChangesAsync();

        // Only Held rows are swept, so a re-run cannot return a hold twice.
        (await sweeper.ExpireStaleReservationsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpireStaleReservationsAsync_Should_BeIdempotent()
    {
        var (db, clock, sweeper) = Create();

        var grant = Grant(5, held: 2);
        var reservation = Reservation(clock.UtcNow.AddMinutes(-1));
        db.EntitlementGrants.Add(grant);
        db.UsageReservations.Add(reservation);
        db.UsageReservationAllocations.Add(new UsageReservationAllocation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            ReservationId = reservation.Id, GrantId = grant.Id, Quantity = 2, Ordinal = 0
        });
        await db.SaveChangesAsync();

        await sweeper.ExpireStaleReservationsAsync();
        await sweeper.ExpireStaleReservationsAsync();

        // A re-run after a partial failure is the intended recovery, so it must not over-return.
        (await db.EntitlementGrants.AsNoTracking().FirstAsync()).Held.Should().Be(0);
    }

    // ---- grant expiry sweep --------------------------------------------------------------

    [Fact]
    public async Task CloseExpiredGrantsAsync_Should_RecordWhenAllowanceLapsed()
    {
        var (db, clock, sweeper) = Create();

        db.EntitlementGrants.Add(Grant(5, 0, expiresAt: clock.UtcNow.AddDays(-1)));
        db.EntitlementGrants.Add(Grant(5, 0, expiresAt: clock.UtcNow.AddDays(1)));
        db.EntitlementGrants.Add(Grant(5, 0, expiresAt: null));
        await db.SaveChangesAsync();

        (await sweeper.CloseExpiredGrantsAsync()).Should().Be(1);

        var closed = await db.EntitlementGrants.AsNoTracking().SingleAsync(g => g.Status == GrantStatuses.Closed);
        closed.ClosedAt.Should().Be(clock.UtcNow, "breakage is a recorded event, not a date comparison re-run later");

        // A never-expiring purchased grant must never be swept — that is the asymmetry the two
        // grant sources exist to express.
        (await db.EntitlementGrants.AsNoTracking().CountAsync(g => g.Status == GrantStatuses.Open)).Should().Be(2);
    }

    [Fact]
    public async Task CloseExpiredGrantsAsync_Should_BeIdempotent()
    {
        var (db, clock, sweeper) = Create();
        db.EntitlementGrants.Add(Grant(5, 0, expiresAt: clock.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        (await sweeper.CloseExpiredGrantsAsync()).Should().Be(1);
        (await sweeper.CloseExpiredGrantsAsync()).Should().Be(0, "only Open grants are closed");
    }

    [Fact]
    public async Task CloseExpiredGrantsAsync_Should_NotTouchHeldUnits()
    {
        var (db, clock, sweeper) = Create();
        db.EntitlementGrants.Add(Grant(5, held: 2, expiresAt: clock.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        await sweeper.CloseExpiredGrantsAsync();

        // A grant still holding units when it expires means a reservation outlived it. The
        // reservation sweep releases that hold; zeroing it here too would double-return.
        (await db.EntitlementGrants.AsNoTracking().FirstAsync()).Held.Should().Be(2);
    }
}
