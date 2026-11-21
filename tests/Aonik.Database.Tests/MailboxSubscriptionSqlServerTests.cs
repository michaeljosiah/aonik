using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class MailboxSubscriptionSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task CursorCommit_Should_RollBackItsDiscoveredEvents_WhenDisableWinsTheRowversionRace()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(); Guid subscriptionId;
        await using (PlatformDbContext seed = Context(tenantId))
        {
            var subscription = Subscription(tenantId); seed.ConnectorSubscriptions.Add(subscription); await seed.SaveChangesAsync(); subscriptionId = subscription.Id;
        }
        await using PlatformDbContext stale = Context(tenantId);
        ConnectorSubscription previous = await stale.ConnectorSubscriptions.SingleAsync(value => value.Id == subscriptionId);
        await using (PlatformDbContext concurrent = Context(tenantId))
        {
            (await concurrent.ConnectorSubscriptions.SingleAsync(value => value.Id == subscriptionId)).Status = "Disabled";
            await concurrent.SaveChangesAsync();
        }
        previous.HistoryId = "200";
        stale.EnqueueIntegrationEvent(new ConnectorMessageAddedEvent(Guid.NewGuid(), DateTime.UtcNow, tenantId, previous.ConnectionId,
            "mail-one", "thread-one", DateTime.UtcNow, ["INBOX"]));

        Func<Task> commit = () => stale.SaveChangesAsync();

        await commit.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using PlatformDbContext verify = Context(tenantId);
        ConnectorSubscription stored = await verify.ConnectorSubscriptions.SingleAsync(value => value.Id == subscriptionId);
        stored.HistoryId.Should().Be("100"); stored.Status.Should().Be("Disabled");
        (await verify.Set<OutboxMessage>().CountAsync(value => value.TenantId == tenantId)).Should().Be(0);
    }

    [SkippableFact]
    public async Task ProviderWatchClaim_Should_BeUniqueAcrossTenants_WhilePollingSubscriptionsRemainIndependent()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid firstTenant = Guid.NewGuid(), otherTenant = Guid.NewGuid(); string claim = Guid.NewGuid().ToString("N");
        await using PlatformDbContext first = Context(firstTenant); await using PlatformDbContext second = Context(otherTenant);
        ConnectorSubscription one = Subscription(firstTenant); one.WatchClaim = claim;
        ConnectorSubscription two = Subscription(otherTenant); two.WatchClaim = claim;
        first.ConnectorSubscriptions.Add(one); second.ConnectorSubscriptions.Add(two); await first.SaveChangesAsync();

        Func<Task> overwrite = () => second.SaveChangesAsync();

        await overwrite.Should().ThrowAsync<DbUpdateException>();
        two.WatchClaim = null; two.Mode = "Polling"; await second.SaveChangesAsync();
        (await second.ConnectorSubscriptions.CountAsync()).Should().Be(1);
        (await first.ConnectorSubscriptions.SingleAsync()).WatchClaim.Should().Be(claim);
    }

    private PlatformDbContext Context(Guid tenantId) => new(database.CreateOptions<PlatformDbContext>(), new TestTenantProvider(tenantId));
    private static ConnectorSubscription Subscription(Guid tenantId) => new() { TenantId = tenantId, ConnectionId = Guid.NewGuid(), ConnectorKey = "gmail",
        ApplicationKey = "reviewed-app", ProviderAccountHash = Guid.NewGuid().ToString("N"), HistoryId = "100", PollTaskId = Guid.NewGuid(),
        PollTaskStartAtUtc = DateTime.UtcNow, PollTaskEndAtUtc = DateTime.UtcNow.AddDays(365) };
}
