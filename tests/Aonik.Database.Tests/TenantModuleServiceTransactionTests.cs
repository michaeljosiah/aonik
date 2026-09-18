using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Database.Tests;

/// <summary>
/// Codex P1-2 on Spec 097 §9, on a real engine: the toggle rows, the outbox message and the audit
/// record are one unit of work. The InMemory provider has no transactions, so "the audit row was saved
/// and then something failed before the commit" can only roll back here — and only here can a
/// <c>BeginTransactionAsync</c> outside the execution strategy throw under <c>EnableRetryOnFailure</c>.
/// The real <see cref="AuditLogWriter"/> is used over the same context the service writes with, exactly
/// as the request scope wires it.
/// </summary>
public class TenantModuleServiceTransactionTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public TenantModuleServiceTransactionTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task UpdateAsync_Should_CommitRowsOutboxAndAuditTogether_When_RunOnSqlServerWithRetryStrategy()
    {
        RequireSqlServer();
        var harness = await Harness.CreateAsync(_db);
        var service = harness.BuildService(harness.RealAuditWriter);

        await service.UpdateAsync(harness.TenantId, [new TenantModuleToggle(ModuleIds.Commerce, false, "no shop")]);

        await using var verify = harness.NewContext();
        (await verify.TenantModules.Where(row => row.TenantId == harness.TenantId).ToListAsync())
            .Should().ContainSingle(row => row.ModuleId == ModuleIds.Commerce && !row.IsEnabled);
        (await verify.Set<OutboxMessage>().Where(message => message.TenantId == harness.TenantId).ToListAsync())
            .Should().ContainSingle(message => message.EventType == typeof(TenantModulesChangedEvent).FullName);
        (await verify.AuditLogs.Where(log => log.TenantId == harness.TenantId).ToListAsync())
            .Should().ContainSingle(log => log.Action == AuditEventNames.TenantModulesUpdated);
        harness.Bus.Published.Should().ContainSingle();
    }

    [SkippableFact]
    public async Task UpdateAsync_Should_RollBackRowsOutboxAndAudit_When_AFailureFollowsTheAuditSave()
    {
        RequireSqlServer();
        var harness = await Harness.CreateAsync(_db);
        // The real writer saves the audit row (and, through the shared context, the rows and the outbox
        // message) — then the request dies before the commit: cancellation, a downstream fault, anything.
        var service = harness.BuildService(new FailAfterWriter(harness.RealAuditWriter));

        var act = () => service.UpdateAsync(harness.TenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("request aborted after the audit save");
        await using var verify = harness.NewContext();
        (await verify.TenantModules.Where(row => row.TenantId == harness.TenantId).ToListAsync())
            .Should().BeEmpty("the transaction rolled back the rows");
        (await verify.Set<OutboxMessage>().Where(message => message.TenantId == harness.TenantId).ToListAsync())
            .Should().BeEmpty("the transaction rolled back the outbox message");
        (await verify.AuditLogs.Where(log => log.TenantId == harness.TenantId).ToListAsync())
            .Should().BeEmpty("the audit row that had already been saved rolled back with the rest");
        harness.Bus.Published.Should().BeEmpty("nothing committed, nothing announced");
    }

    [SkippableFact]
    public async Task UpdateAsync_Should_RollBackRowsAndOutbox_When_TheAuditWriterThrowsBeforeSaving()
    {
        RequireSqlServer();
        var harness = await Harness.CreateAsync(_db);
        var service = harness.BuildService(new ThrowingWriter());

        var act = () => service.UpdateAsync(harness.TenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit store unavailable");
        await using var verify = harness.NewContext();
        (await verify.TenantModules.Where(row => row.TenantId == harness.TenantId).ToListAsync()).Should().BeEmpty();
        (await verify.Set<OutboxMessage>().Where(message => message.TenantId == harness.TenantId).ToListAsync()).Should().BeEmpty();
        (await verify.AuditLogs.Where(log => log.TenantId == harness.TenantId).ToListAsync()).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task UpdateAsync_Should_PersistOnlyTheFailureAudit_When_AProvisioningContributorThrows()
    {
        RequireSqlServer();
        var harness = await Harness.CreateAsync(_db);
        var service = harness.BuildService(harness.RealAuditWriter, new ThrowingContributor(ModuleIds.Commerce));
        // Commerce off, then on again: the off→on transition is what runs the contributor.
        await service.UpdateAsync(harness.TenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);
        harness.Bus.Published.Clear();

        var act = () => service.UpdateAsync(harness.TenantId, [new TenantModuleToggle(ModuleIds.Commerce, true)]);

        await act.Should().ThrowAsync<ModuleProvisioningException>();
        await using var verify = harness.NewContext();
        (await verify.TenantModules.Where(row => row.TenantId == harness.TenantId).ToListAsync())
            .Should().ContainSingle(row => row.ModuleId == ModuleIds.Commerce && !row.IsEnabled,
                "the failure audit's own SaveChanges must not carry the toggle with it on a real engine");
        (await verify.Set<OutboxMessage>().Where(message => message.TenantId == harness.TenantId).ToListAsync())
            .Should().ContainSingle("only the first, successful toggle enqueued an event");
        // The clock is fixed, so both rows carry the same timestamp; only the set is meaningful.
        var audits = await verify.AuditLogs.Where(log => log.TenantId == harness.TenantId).ToListAsync();
        audits.Select(log => log.Action).Should().BeEquivalentTo([AuditEventNames.TenantModulesUpdated, TenantModuleService.ProvisioningFailedAuditAction]);
        harness.Bus.Published.Should().BeEmpty();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");

    // ── harness ─────────────────────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        private readonly SqlLocalDbFixture _db;
        private readonly TestCurrentUserProvider _user = new(Guid.NewGuid());
        private readonly FixedClock _clock = new();
        private readonly FixedCorrelationContext _correlation = new("corr-db-097");

        /// <summary>The scoped context: the service AND the real audit writer share it, as in a request.</summary>
        private readonly PlatformDbContext _context;

        private Harness(SqlLocalDbFixture db, Guid tenantId)
        {
            _db = db;
            TenantId = tenantId;
            _context = NewContext();
            RealAuditWriter = new AuditLogWriter(_context, new TestTenantProvider(tenantId), _user, _correlation, _clock);
        }

        public Guid TenantId { get; }
        public RecordingEventBus Bus { get; } = new();
        public IAuditLogWriter RealAuditWriter { get; }

        public static async Task<Harness> CreateAsync(SqlLocalDbFixture db)
        {
            var tenantId = Guid.NewGuid();
            var harness = new Harness(db, tenantId);
            await using var seed = harness.NewContext();
            // One database per class, one tenant per test: the name must be unique too (AnkTenants indexes it).
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = $"Module Tenant {tenantId:N}",
                Subdomain = $"modules-{tenantId:N}",
                Environment = "Development",
                DefaultCurrency = "GBP",
                SupportedCountriesJson = "[]",
                Status = TenantStatus.Active,
            });
            await seed.SaveChangesAsync();
            return harness;
        }

        public PlatformDbContext NewContext()
            => new(_db.CreateOptions<PlatformDbContext>(), new TestTenantProvider(TenantId), _user, _clock);

        public ITenantModuleService BuildService(IAuditLogWriter auditWriter, params ITenantProvisioningContributor[] contributors)
        {
            var services = new ServiceCollection();
            foreach (var contributor in contributors)
                services.AddSingleton(contributor);

            return new TenantModuleService(
                _context,
                new FusionCache(new FusionCacheOptions()),
                NullLogger<TenantModuleService>.Instance,
                _clock,
                _user,
                _correlation,
                auditWriter,
                Bus,
                new FakeTenantContext { TenantId = TenantId, ResolutionSource = "Test" },
                new AllowAllPermissionService(),
                services.BuildServiceProvider());
        }
    }

    // ── fakes ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Delegates to the real writer (which saves through the shared context), then fails.</summary>
    private sealed class FailAfterWriter(IAuditLogWriter inner) : IAuditLogWriter
    {
        public async Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            await inner.LogAsync(action, resourceType, resourceId, tenantId, actorId, correlationId, detailsJson, cancellationToken);
            throw new InvalidOperationException("request aborted after the audit save");
        }
    }

    private sealed class ThrowingWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("audit store unavailable");
    }

    private sealed class ThrowingContributor(string moduleName) : ITenantProvisioningContributor
    {
        public string ModuleName => moduleName;

        public Task<TenantProvisioningContribution> ContributeProvisioningAsync(TenantProvisioningContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("catalogue store unavailable");

        public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FixedCorrelationContext(string correlationId) : ICorrelationContext
    {
        public string? CorrelationId => correlationId;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }
}
