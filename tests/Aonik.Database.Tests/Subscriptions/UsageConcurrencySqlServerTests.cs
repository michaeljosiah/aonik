using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Subscriptions;

/// <summary>
/// Spec 087 §15 — the concurrency and uniqueness guarantees, against a real engine.
///
/// This lane is mandatory for this spec rather than optional. The InMemory provider is
/// non-relational: it enforces no unique index and honours no <c>RowVersion</c> concurrency token,
/// so <b>every test here would pass against it whether or not the protection existed</b>. The
/// races these cover — two holds taking the last unit, a period materialising its grants twice —
/// each end in a subscriber getting allowance nobody granted, or being charged for it twice.
/// </summary>
public class UsageConcurrencySqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public UsageConcurrencySqlServerTests(SqlLocalDbFixture db) => _db = db;

    private SubscriptionsDbContext CreateContext(Guid tenantId)
        => new(_db.CreateOptions<SubscriptionsDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    private static EntitlementGrant NewGrant(
        Guid tenantId, Guid subscriberId, decimal allowance,
        Guid? periodId = null, string source = GrantSources.Plan, DateTime? expiresAt = null) => new()
    {
        TenantId = tenantId,
        SubscriberKind = SubscriberKinds.Group,
        SubscriberId = subscriberId,
        PeriodId = periodId,
        MeterCode = "stories",
        Source = source,
        Allowance = allowance,
        Consumed = 0,
        Held = 0,
        ExpiresAt = expiresAt,
        Status = GrantStatuses.Open
    };

    // ---- RowVersion: the race the Held column exists to catch -----------------------------

    [SkippableFact]
    public async Task ConcurrentHolds_OnTheLastUnit_Should_LetExactlyOneSucceed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        Guid grantId;
        await using (var seed = CreateContext(tenantId))
        {
            var grant = NewGrant(tenantId, subscriberId, allowance: 1);
            seed.EntitlementGrants.Add(grant);
            await seed.SaveChangesAsync();
            grantId = grant.Id;
        }

        // Two callers read the same grant, both see one unit free, both try to hold it.
        await using var a = CreateContext(tenantId);
        await using var b = CreateContext(tenantId);

        var grantA = await a.EntitlementGrants.FirstAsync(g => g.Id == grantId);
        var grantB = await b.EntitlementGrants.FirstAsync(g => g.Id == grantId);

        grantA.Held += 1;
        await a.SaveChangesAsync();

        grantB.Held += 1;
        var act = () => b.SaveChangesAsync();

        // RowVersion is the whole protection. Without the Held write there would be nothing to
        // conflict on and both would "succeed", handing out an allowance that does not exist.
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = CreateContext(tenantId);
        (await verify.EntitlementGrants.AsNoTracking().FirstAsync(g => g.Id == grantId))
            .Held.Should().Be(1);
    }

    [SkippableFact]
    public async Task ConcurrentCommits_OnTheLastUnit_Should_LetExactlyOneSucceed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        Guid grantId;
        await using (var seed = CreateContext(tenantId))
        {
            var grant = NewGrant(tenantId, subscriberId, allowance: 1);
            seed.EntitlementGrants.Add(grant);
            await seed.SaveChangesAsync();
            grantId = grant.Id;
        }

        await using var a = CreateContext(tenantId);
        await using var b = CreateContext(tenantId);

        var grantA = await a.EntitlementGrants.FirstAsync(g => g.Id == grantId);
        var grantB = await b.EntitlementGrants.FirstAsync(g => g.Id == grantId);

        grantA.Consumed += 1;
        await a.SaveChangesAsync();

        grantB.Consumed += 1;
        var act = () => b.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ---- grant materialisation: at-least-once payment events -------------------------------

    [SkippableFact]
    public async Task MaterialisingAPeriodTwice_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.EntitlementGrants.Add(NewGrant(tenantId, subscriberId, 6, periodId));
        await ctx.SaveChangesAsync();

        ctx.EntitlementGrants.Add(NewGrant(tenantId, subscriberId, 6, periodId));

        // Payment completion is at-least-once. A status check alone cannot carry this: a retried
        // or concurrently-handled event would silently DOUBLE the subscriber's allowance.
        var thrown = await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>();
        var sql = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sql.Number.Should().BeOneOf([2601, 2627]);
        sql.Message.Should().Contain("IX_AnkEntitlementGrants_PeriodMeterSource_Unique");
    }

    [SkippableFact]
    public async Task PurchasedGrants_WithNoPeriod_Should_NotCollide()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.EntitlementGrants.Add(NewGrant(tenantId, subscriberId, 5, periodId: null, source: GrantSources.Purchase));
        ctx.EntitlementGrants.Add(NewGrant(tenantId, subscriberId, 5, periodId: null, source: GrantSources.Purchase));

        // A subscriber may buy top-ups repeatedly. The period index is filtered on PeriodId IS NOT
        // NULL precisely so purchases are unconstrained by it.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task TwoPurchasedGrants_ForTheSameOrderLine_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);

        var first = NewGrant(tenantId, subscriberId, 5, periodId: null, source: GrantSources.Purchase);
        first.SourceOrderId = orderId;
        ctx.EntitlementGrants.Add(first);
        await ctx.SaveChangesAsync();

        var second = NewGrant(tenantId, subscriberId, 5, periodId: null, source: GrantSources.Purchase);
        second.SourceOrderId = orderId;
        ctx.EntitlementGrants.Add(second);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ---- ceilings --------------------------------------------------------------------------

    [SkippableFact]
    public async Task ConcurrentSlotClaims_AtTheLimit_Should_LetExactlyOneSucceed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        Guid holdingId;
        await using (var seed = CreateContext(tenantId))
        {
            var holding = new CeilingHolding
            {
                TenantId = tenantId,
                SubscriberKind = SubscriberKinds.Group,
                SubscriberId = subscriberId,
                MeterCode = "child-profiles",
                Held = 2
            };
            seed.CeilingHoldings.Add(holding);
            await seed.SaveChangesAsync();
            holdingId = holding.Id;
        }

        // Both callers are one slot below a ceiling of 3 and both try to take it.
        await using var a = CreateContext(tenantId);
        await using var b = CreateContext(tenantId);

        var holdingA = await a.CeilingHoldings.FirstAsync(h => h.Id == holdingId);
        var holdingB = await b.CeilingHoldings.FirstAsync(h => h.Id == holdingId);

        holdingA.Held += 1;
        await a.SaveChangesAsync();

        holdingB.Held += 1;
        var act = () => b.SaveChangesAsync();

        // A point-in-time count would let both through — which over-admits under exactly the
        // conditions a ceiling exists for.
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [SkippableFact]
    public async Task TheSameHolder_ClaimingTwice_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        var holding = new CeilingHolding
        {
            TenantId = tenantId,
            SubscriberKind = SubscriberKinds.Group,
            SubscriberId = Guid.NewGuid(),
            MeterCode = "child-profiles",
            Held = 1
        };
        ctx.CeilingHoldings.Add(holding);
        ctx.CeilingClaims.Add(new CeilingClaim
        {
            TenantId = tenantId, CeilingHoldingId = holding.Id, HolderRef = "profile-1", ClaimedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.CeilingClaims.Add(new CeilingClaim
        {
            TenantId = tenantId, CeilingHoldingId = holding.Id, HolderRef = "profile-1", ClaimedAt = DateTime.UtcNow
        });

        // The idempotency guarantee: a retried create must not consume a second slot.
        var thrown = await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("IX_AnkCeilingClaims_Holder_Unique");
    }

    [SkippableFact]
    public async Task TwoHoldingRows_ForOneSubscriberAndMeter_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);

        for (var i = 0; i < 2; i++)
        {
            ctx.CeilingHoldings.Add(new CeilingHolding
            {
                TenantId = tenantId,
                SubscriberKind = SubscriberKinds.Group,
                SubscriberId = subscriberId,
                MeterCode = "child-profiles",
                Held = 0
            });
        }

        // Two counter rows would each believe they hold the whole allowance, doubling the ceiling.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ---- reservation idempotency ------------------------------------------------------------

    [SkippableFact]
    public async Task TwoReservations_WithTheSameKey_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);

        for (var i = 0; i < 2; i++)
        {
            ctx.UsageReservations.Add(new UsageReservation
            {
                TenantId = tenantId,
                SubscriberKind = SubscriberKinds.Group,
                SubscriberId = subscriberId,
                MeterCode = "stories",
                Quantity = 1,
                Status = UsageReservationStatuses.Held,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IdempotencyKey = "story-1"
            });
        }

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task TheSameReservationKey_InAnotherTenant_Should_NotCollide()
    {
        RequireSqlServer();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        foreach (var tenantId in new[] { tenantA, tenantB })
        {
            await using var ctx = CreateContext(tenantId);
            ctx.UsageReservations.Add(new UsageReservation
            {
                TenantId = tenantId,
                SubscriberKind = SubscriberKinds.Group,
                SubscriberId = Guid.NewGuid(),
                MeterCode = "stories",
                Quantity = 1,
                Status = UsageReservationStatuses.Held,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IdempotencyKey = "story-1"
            });

            // Keys are client-generated, so two tenants will collide eventually. A global
            // constraint would fail the second against a row its query filter cannot even see.
            var act = () => ctx.SaveChangesAsync();
            await act.Should().NotThrowAsync();
        }
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
