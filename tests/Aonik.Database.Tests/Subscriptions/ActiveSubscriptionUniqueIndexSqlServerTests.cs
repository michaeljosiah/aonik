using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Subscriptions;
using Aonik.Subscriptions.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Subscriptions;

/// <summary>
/// Spec 087 §17.1 — the one-active-subscription-per-subscriber invariant, enforced by the filtered
/// unique index <c>IX_AnkSubscriptions_ActiveSubscriber_Unique</c>.
///
/// Open decision O4 originally left this to a service check. It cannot be: two concurrent
/// <c>Subscribe</c> calls both pass a service check and both insert, and the subscriber is then
/// renewed — and charged — twice. Only the engine can hold it, and the InMemory provider ignores
/// index definitions entirely, so this lane is the only place the constraint is real.
/// </summary>
public class ActiveSubscriptionUniqueIndexSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private const string IndexName = "IX_AnkSubscriptions_ActiveSubscriber_Unique";

    private readonly SqlLocalDbFixture _db;

    public ActiveSubscriptionUniqueIndexSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private SubscriptionsDbContext CreateContext(Guid tenantId)
        => new(_db.CreateOptions<SubscriptionsDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    private static Subscription NewSubscription(Guid tenantId, Guid subscriberId, string status) => new()
    {
        TenantId = tenantId,
        SubscriberKind = SubscriberKinds.Group,
        SubscriberId = subscriberId,
        PlanVersionId = Guid.NewGuid(),
        Status = status,
        CurrentPeriodStart = DateTime.UtcNow,
        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
        StartedAt = DateTime.UtcNow
    };

    [SkippableFact]
    public async Task ASecondActiveSubscription_ForOneSubscriber_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Active));
        await ctx.SaveChangesAsync();

        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Active));

        var act = () => ctx.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var sql = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sql.Number.Should().BeOneOf([2601, 2627]);
        sql.Message.Should().Contain(IndexName);
    }

    [SkippableFact]
    public async Task TrialingAndPastDue_Should_AlsoOccupyTheSlot()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Trialing));
        await ctx.SaveChangesAsync();

        // past_due is still a live subscription being chased for payment, not a free slot.
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.PastDue));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task ManyCancelledSubscriptions_Should_NotCollide()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Cancelled));
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Cancelled));
        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Expired));

        // A subscriber who leaves and returns repeatedly accumulates history. Only the LIVE slot
        // is exclusive — an unfiltered index would make re-subscribing impossible after the first
        // cancellation.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task ResubscribingAfterCancelling_Should_Succeed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        var first = NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Active);
        ctx.Subscriptions.Add(first);
        await ctx.SaveChangesAsync();

        first.Status = SubscriptionStatuses.Cancelled;
        await ctx.SaveChangesAsync();

        ctx.Subscriptions.Add(NewSubscription(tenantId, subscriberId, SubscriptionStatuses.Active));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("the cancelled predecessor is outside the index filter");
    }

    [SkippableFact]
    public async Task TheSameSubscriberId_InAnotherTenant_Should_NotCollide()
    {
        RequireSqlServer();
        var subscriberId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var ctx = CreateContext(tenantA))
        {
            ctx.Subscriptions.Add(NewSubscription(tenantA, subscriberId, SubscriptionStatuses.Active));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(tenantB))
        {
            ctx.Subscriptions.Add(NewSubscription(tenantB, subscriberId, SubscriptionStatuses.Active));
            var act = () => ctx.SaveChangesAsync();
            await act.Should().NotThrowAsync("the invariant is per tenant");
        }
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
