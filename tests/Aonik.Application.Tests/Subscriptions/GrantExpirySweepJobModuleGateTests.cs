using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
using Aonik.Subscriptions.Entities.Usage;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Usage;
using Aonik.Worker.Jobs;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Quartz;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 097 §12.2 / acceptance 11 for a Subscriptions job: <see cref="GrantExpirySweepJob"/> runs
/// the real <see cref="UsageSweeper"/> per tenant, skips a tenant whose Subscriptions module is
/// off (its lapsed grant stays open, nothing is written for it) and records the skip.
/// </summary>
public class GrantExpirySweepJobModuleGateTests
{
    private static readonly Guid EnabledTenant = Guid.NewGuid();
    private static readonly Guid DisabledTenant = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Execute_Should_CloseExpiredGrantsOnlyForEnabledTenants_AndRecordTheSkip()
    {
        // Arrange
        var tenantContext = new MutableTenantContext();
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new FixedClock(Now);
        await using var db = new SubscriptionsDbContext(
            new DbContextOptionsBuilder<SubscriptionsDbContext>().UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options,
            tenantProvider,
            clock: clock);

        var enabledGrant = await SeedExpiredGrantAsync(db, tenantContext, EnabledTenant);
        var disabledGrant = await SeedExpiredGrantAsync(db, tenantContext, DisabledTenant);

        var job = new GrantExpirySweepJob(
            new UsageSweeper(db, tenantProvider, clock),
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions()),
            NullLogger<GrantExpirySweepJob>.Instance,
            new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        db.ChangeTracker.Clear();
        var grants = await db.EntitlementGrants.AcrossTenants().AsNoTracking().ToListAsync();
        grants.Single(g => g.Id == enabledGrant).Status.Should().Be(GrantStatuses.Closed, "the enabled tenant was swept");
        grants.Single(g => g.Id == disabledGrant).Status.Should().Be(GrantStatuses.Open, "a disabled tenant's rows are never touched");
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Closed 1")
            .And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Subscriptions}' disabled");
        tenantContext.TenantId.Should().BeNull("the job resets the ambient tenant after each tenant");
    }

    private static async Task<Guid> SeedExpiredGrantAsync(SubscriptionsDbContext db, MutableTenantContext tenantContext, Guid tenantId)
    {
        // EnforceTenantOnWrites: the ambient tenant must match the rows being saved.
        tenantContext.TenantId = tenantId;
        var grant = new EntitlementGrant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = SubscriberKinds.Tenant,
            SubscriberId = tenantId,
            MeterCode = "stories",
            Source = GrantSources.Plan,
            Allowance = 5,
            Consumed = 0,
            Held = 0,
            ExpiresAt = Now.AddDays(-1),
            Status = GrantStatuses.Open,
        };
        db.EntitlementGrants.Add(grant);
        await db.SaveChangesAsync();
        tenantContext.TenantId = null;
        return grant.Id;
    }

    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(c => c.Result);
        return mock.Object;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider(ITenantContext context) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => context.TenantId ?? throw new InvalidOperationException("Tenant context not available");

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = context.TenantId ?? Guid.Empty;
            return context.TenantId.HasValue;
        }
    }

    /// <summary>Every module on for every tenant except Subscriptions for the given tenants.</summary>
    private sealed class FakeReader(params Guid[] subscriptionsOffFor) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (subscriptionsOffFor.Contains(tenantId))
                enabled.Remove(ModuleIds.Subscriptions);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = moduleId == ModuleIds.Subscriptions
                ? tenantIds.Distinct().Where(id => !subscriptionsOffFor.Contains(id)).ToList()
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}

