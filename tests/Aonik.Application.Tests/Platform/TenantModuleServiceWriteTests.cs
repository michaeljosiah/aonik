using System.Text.Json;

using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 097 §9 / §16: the admin side of per-tenant module enablement — the catalogue projection with
/// provenance, the toggle validation order (unknown, core, duplicate, missing dependency, enabled
/// dependent), the upsert, the audit record, the published event and the cache invalidation.
/// </summary>
public class TenantModuleServiceWriteTests
{
    private static readonly DateTime FixedNow = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<PlatformDbContext> _options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    private readonly IFusionCache _cache = new FusionCache(new FusionCacheOptions());
    private readonly RecordingAuditLogWriter _audit = new();
    private readonly RecordingEventBus _bus = new();
    private readonly TestCurrentUserProvider _user = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public TenantModuleServiceWriteTests()
    {
        SeedTenant(_tenantId);
    }

    /// <summary>Builds the service with its ambient tenant set to <paramref name="ambientTenantId"/> (defaults to the test tenant).</summary>
    private TenantModuleService CreateService(
        Guid? ambientTenantId = null,
        IPermissionService? permissions = null,
        IAuditLogWriter? audit = null,
        IEventBus? bus = null)
    {
        var ambient = ambientTenantId ?? _tenantId;
        return new TenantModuleService(
            new PlatformDbContext(_options, new TestTenantProvider(ambient), _user, new FixedClock(FixedNow)),
            _cache,
            NullLogger<TenantModuleService>.Instance,
            new FixedClock(FixedNow),
            _user,
            new FixedCorrelationContext("corr-1"),
            audit ?? _audit,
            bus ?? _bus,
            new FakeTenantContext { TenantId = ambient, ResolutionSource = "Test" },
            permissions ?? new AllowAllPermissionService());
    }

    private ITenantModuleService CreateAdminService(
        Guid? ambientTenantId = null,
        IPermissionService? permissions = null,
        IAuditLogWriter? audit = null,
        IEventBus? bus = null)
        => CreateService(ambientTenantId, permissions, audit, bus);

    private async Task<List<Aonik.SharedKernel.Events.Outbox.OutboxMessage>> LoadOutboxAsync()
    {
        await using var context = new PlatformDbContext(_options, new TestTenantProvider(_tenantId), _user);
        return await context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().ToListAsync();
    }

    private void SeedTenant(Guid tenantId)
    {
        using var context = new PlatformDbContext(_options, new TestTenantProvider(tenantId), _user);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Module Tenant",
            Environment = "Development",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        context.SaveChanges();
    }

    private async Task SeedRowAsync(Guid tenantId, string moduleId, bool isEnabled, string source = TenantModuleSource.Explicit, string? reason = null)
    {
        // The context stamps CreatedAt itself, so the seed clock is what makes the row look a day old.
        await using var context = new PlatformDbContext(_options, new TestTenantProvider(tenantId), _user, new FixedClock(FixedNow.AddDays(-1)));
        context.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = source,
            Reason = reason,
            CreatedAt = FixedNow.AddDays(-1),
        });
        await context.SaveChangesAsync();
    }

    private async Task<List<TenantModule>> LoadRowsAsync(Guid tenantId)
    {
        await using var context = new PlatformDbContext(_options, new TestTenantProvider(tenantId), _user);
        return await context.TenantModules.Where(row => row.TenantId == tenantId).ToListAsync();
    }

    // ── GetAsync ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Should_ReportEveryCatalogueModule_When_TenantHasNoRows()
    {
        var service = CreateAdminService();

        var list = await service.GetAsync(_tenantId);

        list.TenantId.Should().Be(_tenantId);
        list.Modules.Select(module => module.ModuleId).Should().Equal(ModuleCatalog.All.Select(descriptor => descriptor.Id));
        list.Modules.Should().OnlyContain(module => module.IsEnabled, "every shipped module defaults to on");
    }

    [Fact]
    public async Task GetAsync_Should_ReportSource_ForCoreDefaultPackAndExplicitModules()
    {
        await SeedRowAsync(_tenantId, ModuleIds.Commerce, isEnabled: false, source: TenantModuleSource.Pack, reason: "pack says no");
        await SeedRowAsync(_tenantId, ModuleIds.Groups, isEnabled: false, source: TenantModuleSource.Explicit, reason: "admin says no");
        var service = CreateAdminService();

        var list = await service.GetAsync(_tenantId);
        var byId = list.Modules.ToDictionary(module => module.ModuleId);

        byId[ModuleIds.Platform].Source.Should().Be(TenantModuleStateSource.Core);
        byId[ModuleIds.Platform].IsCore.Should().BeTrue();
        byId[ModuleIds.Platform].IsEnabled.Should().BeTrue();

        byId[ModuleIds.Finance].Source.Should().Be(TenantModuleStateSource.Default);
        byId[ModuleIds.Finance].IsEnabled.Should().BeTrue();
        byId[ModuleIds.Finance].UpdatedAt.Should().BeNull("no row exists");

        byId[ModuleIds.Commerce].Source.Should().Be(TenantModuleStateSource.Pack);
        byId[ModuleIds.Commerce].IsEnabled.Should().BeFalse();
        byId[ModuleIds.Commerce].Reason.Should().Be("pack says no");
        byId[ModuleIds.Commerce].UpdatedAt.Should().Be(FixedNow.AddDays(-1), "UpdatedAt falls back to CreatedAt");

        byId[ModuleIds.Groups].Source.Should().Be(TenantModuleStateSource.Explicit);
        byId[ModuleIds.Groups].Reason.Should().Be("admin says no");
        byId[ModuleIds.Groups].DependsOn.Should().BeEmpty();
        byId[ModuleIds.Workspaces].IsEnabled.Should().BeFalse("workspaces hard-depends on groups, so the closure switches it off");
        byId[ModuleIds.Workspaces].Source.Should().Be(TenantModuleStateSource.Default, "closure changes the state, not the provenance");
    }

    [Fact]
    public async Task GetAsync_Should_Throw_When_ReadingAnotherTenantWithoutPermission()
    {
        var service = CreateAdminService(ambientTenantId: Guid.NewGuid(), permissions: new DenyAllPermissionService());

        var act = () => service.GetAsync(_tenantId);

        await act.Should().ThrowAsync<PermissionDeniedException>("a tenant admin may only read their own tenant");
    }

    [Fact]
    public async Task GetAsync_Should_Throw_When_TenantDoesNotExist()
    {
        var missing = Guid.NewGuid();
        var service = CreateAdminService(ambientTenantId: missing);

        var act = () => service.GetAsync(missing);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateAsync: validation ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ModuleIds.Platform)]
    [InlineData(ModuleIds.Ordering)]
    [InlineData(ModuleIds.Ai)]
    [InlineData(ModuleIds.Agents)]
    public async Task UpdateAsync_Should_RejectCoreModule(string coreModuleId)
    {
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(coreModuleId, false)]);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*core*");
        _audit.Entries.Should().BeEmpty();
        _bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_Should_RejectUnknownModule()
    {
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle("not-a-module", false)]);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not a module*");
    }

    [Fact]
    public async Task UpdateAsync_Should_RejectDuplicateModuleIds()
    {
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Commerce, false),
            new TenantModuleToggle(ModuleIds.Commerce, true),
        ]);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*more than once*");
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowDependencyMissing_When_EnablingCommerceWithFinanceOff()
    {
        await SeedRowAsync(_tenantId, ModuleIds.Finance, isEnabled: false);
        await SeedRowAsync(_tenantId, ModuleIds.Commerce, isEnabled: false);
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, true)]);

        var thrown = await act.Should().ThrowAsync<ModuleDependencyException>();
        thrown.Which.Code.Should().Be(ModuleErrorCodes.DependencyMissing);
        thrown.Which.ModuleId.Should().Be(ModuleIds.Commerce);
        thrown.Which.RelatedModuleIds.Should().Equal([ModuleIds.Finance], "ordering is core and therefore on; only finance is missing");
        (await LoadRowsAsync(_tenantId)).Single(row => row.ModuleId == ModuleIds.Commerce).IsEnabled.Should().BeFalse("nothing is written on rejection");
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowDependencyMissing_When_EnablingWorkspacesWithSubscriptionsOffInTheSameRequest()
    {
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Subscriptions, false),
            new TenantModuleToggle(ModuleIds.Workspaces, true),
        ]);

        var thrown = await act.Should().ThrowAsync<ModuleDependencyException>();
        thrown.Which.Code.Should().Be(ModuleErrorCodes.DependencyMissing);
        thrown.Which.ModuleId.Should().Be(ModuleIds.Workspaces);
        thrown.Which.RelatedModuleIds.Should().Equal([ModuleIds.Subscriptions], "the request itself switches subscriptions off");
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowDependentsEnabled_When_DisablingFinanceWithCommerceOn()
    {
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, false)]);

        var thrown = await act.Should().ThrowAsync<ModuleDependencyException>();
        thrown.Which.Code.Should().Be(ModuleErrorCodes.DependentsEnabled);
        thrown.Which.ModuleId.Should().Be(ModuleIds.Finance);
        thrown.Which.RelatedModuleIds.Should().Equal(
            ModuleIds.Commerce, ModuleIds.Subscriptions, ModuleIds.Workspaces);
        (await LoadRowsAsync(_tenantId)).Should().BeEmpty("nothing is written on rejection");
    }

    [Fact]
    public async Task UpdateAsync_Should_NotCountADependent_When_TheSameRequestDisablesIt()
    {
        await SeedRowAsync(_tenantId, ModuleIds.Workspaces, isEnabled: false);
        var service = CreateAdminService();

        var act = () => service.UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Finance, false),
            new TenantModuleToggle(ModuleIds.Commerce, false),
        ]);

        var thrown = await act.Should().ThrowAsync<ModuleDependencyException>();
        thrown.Which.Code.Should().Be(ModuleErrorCodes.DependentsEnabled);
        thrown.Which.RelatedModuleIds.Should().Equal([ModuleIds.Subscriptions],
            "commerce is disabled by the same request and workspaces is already off, so only subscriptions blocks");
    }

    // ── UpdateAsync: success ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_DisableFinanceCommerceSubscriptionsAndWorkspacesTogether()
    {
        var service = CreateAdminService();

        var list = await service.UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Commerce, false, "no shop"),
            new TenantModuleToggle(ModuleIds.Subscriptions, false),
            new TenantModuleToggle(ModuleIds.Workspaces, false),
            new TenantModuleToggle(ModuleIds.Finance, false, "no money"),
        ]);

        var byId = list.Modules.ToDictionary(module => module.ModuleId);
        byId[ModuleIds.Finance].IsEnabled.Should().BeFalse();
        byId[ModuleIds.Commerce].IsEnabled.Should().BeFalse();
        byId[ModuleIds.Subscriptions].IsEnabled.Should().BeFalse();
        byId[ModuleIds.Workspaces].IsEnabled.Should().BeFalse();
        byId[ModuleIds.PersonalFinance].IsEnabled.Should().BeTrue("finance is only a soft dependency of personal finance");
        byId[ModuleIds.Finance].Source.Should().Be(TenantModuleStateSource.Explicit);
        byId[ModuleIds.Finance].Reason.Should().Be("no money");
        byId[ModuleIds.Finance].UpdatedAt.Should().Be(FixedNow);
        byId[ModuleIds.Finance].UpdatedBy.Should().Be(_user.UserId);

        var rows = await LoadRowsAsync(_tenantId);
        rows.Should().HaveCount(4);
        rows.Should().OnlyContain(row => row.Source == TenantModuleSource.Explicit && !row.IsEnabled);
    }

    [Fact]
    public async Task UpdateAsync_Should_EnableCommerce_When_FinanceIsOn()
    {
        await SeedRowAsync(_tenantId, ModuleIds.Commerce, isEnabled: false, source: TenantModuleSource.Pack, reason: "pack default");
        var service = CreateAdminService();

        var list = await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, true, "customer asked")]);

        var commerce = list.Modules.Single(module => module.ModuleId == ModuleIds.Commerce);
        commerce.IsEnabled.Should().BeTrue();
        commerce.Source.Should().Be(TenantModuleStateSource.Explicit, "an admin toggle takes over a pack row");
        commerce.Reason.Should().Be("customer asked");

        var rows = await LoadRowsAsync(_tenantId);
        rows.Should().ContainSingle(row => row.ModuleId == ModuleIds.Commerce, "the existing row is updated, not duplicated");
        rows.Single().UpdatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task UpdateAsync_Should_WriteAnAuditRecord_WithBeforeAndAfterPerModule()
    {
        var service = CreateAdminService();

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false, "no shop")]);

        var entry = _audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be(AuditEventNames.TenantModulesUpdated);
        entry.ResourceType.Should().Be("TenantModules");
        entry.ResourceId.Should().Be(_tenantId);
        entry.TenantId.Should().Be(_tenantId);
        entry.ActorId.Should().Be(_user.UserId);
        entry.CorrelationId.Should().Be("corr-1");

        using var payload = JsonDocument.Parse(entry.DetailsJson!);
        var change = payload.RootElement.GetProperty("changes").EnumerateArray().Single();
        change.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Commerce);
        change.GetProperty("before").GetBoolean().Should().BeTrue();
        change.GetProperty("after").GetBoolean().Should().BeFalse();
        change.GetProperty("reason").GetString().Should().Be("no shop");
        payload.RootElement.GetProperty("disabled").EnumerateArray().Select(element => element.GetString())
            .Should().Equal(ModuleIds.Commerce);
    }

    [Fact]
    public async Task UpdateAsync_Should_PublishTenantModulesChangedEvent_WithEnabledAndDisabledLists()
    {
        var service = CreateAdminService();

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, false), new TenantModuleToggle(ModuleIds.Commerce, false), new TenantModuleToggle(ModuleIds.Subscriptions, false), new TenantModuleToggle(ModuleIds.Workspaces, false)]);
        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        _bus.Published.Should().HaveCount(2);
        var first = _bus.Published[0].Should().BeOfType<TenantModulesChangedEvent>().Which;
        first.TenantId.Should().Be(_tenantId);
        first.ChangedBy.Should().Be(_user.UserId);
        first.Enabled.Should().BeEmpty();
        first.Disabled.Should().Equal(ModuleIds.Commerce, ModuleIds.Finance, ModuleIds.Subscriptions, ModuleIds.Workspaces);

        var second = _bus.Published[1].Should().BeOfType<TenantModulesChangedEvent>().Which;
        second.Enabled.Should().Equal(ModuleIds.Finance);
        second.Disabled.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_Should_EnqueueTheEventOnTheOutbox()
    {
        var service = CreateAdminService();

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        await using var context = new PlatformDbContext(_options, new TestTenantProvider(_tenantId), _user);
        var outbox = await context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().ToListAsync();
        outbox.Should().ContainSingle(message => message.EventType == typeof(TenantModulesChangedEvent).FullName);
    }

    [Fact]
    public async Task UpdateAsync_Should_InvalidateTheCache_So_TheReaderSeesTheChange()
    {
        IModuleEnablementReader reader = CreateService();
        (await reader.GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue("warm the cache first");

        await CreateAdminService().UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        IModuleEnablementReader fresh = CreateService();
        (await fresh.GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeFalse(
            "the write path drops the cached set itself rather than waiting on the event handler");
    }

    // ── UpdateAsync: atomicity of rows, outbox and audit (Codex P1-2) ───────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_PersistNothingAndKeepTheCache_When_TheAuditWriterThrows()
    {
        IModuleEnablementReader reader = CreateService();
        (await reader.GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue("warm the cache first");
        var service = CreateAdminService(audit: new ThrowingAuditLogWriter());

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false, "no shop")]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit store unavailable");
        (await LoadRowsAsync(_tenantId)).Should().BeEmpty("the toggle rides the same unit of work as the audit record");
        (await LoadOutboxAsync()).Should().BeEmpty("the outbox message rides the same unit of work as the audit record");
        _bus.Published.Should().BeEmpty("nothing changed, so nothing is announced");

        var cached = await _cache.TryGetAsync<IReadOnlySet<string>>(TenantModuleService.CacheKey(_tenantId));
        cached.HasValue.Should().BeTrue("a rolled-back change must not invalidate the cache");
        cached.Value.Should().Contain(ModuleIds.Commerce);
    }

    [Fact]
    public async Task UpdateAsync_Should_WriteOneAuditRecord_When_AFailedAttemptLeftOneOnTheTracker()
    {
        // PlatformDbContext enables EnableRetryOnFailure, so the commit delegate is replayed on a
        // transient failure. A rollback does not untrack, so the audit row the failed attempt added is
        // still Added when the retry runs — the exact state seeded here. Without the detach the retry
        // commits two audit rows for one toggle. A real transient failure cannot be induced on the
        // InMemory provider (its execution strategy never retries), so the precondition is seeded
        // directly; the SQL Server lane covers the transaction itself.
        var dbContext = new PlatformDbContext(_options, new TestTenantProvider(_tenantId), _user, new FixedClock(FixedNow));
        var service = new TenantModuleService(
            dbContext,
            _cache,
            NullLogger<TenantModuleService>.Instance,
            new FixedClock(FixedNow),
            _user,
            new FixedCorrelationContext("corr-1"),
            new ContextWritingAuditLogWriter(dbContext, FixedNow),
            _bus,
            new FakeTenantContext { TenantId = _tenantId, ResolutionSource = "Test" },
            new AllowAllPermissionService());

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Timestamp = FixedNow,
            ActorType = "User",
            ActorId = _user.UserId,
            Action = AuditEventNames.TenantModulesUpdated,
            ResourceType = "TenantModules",
            ResourceId = _tenantId,
            DetailsJson = "{\"attempt\":1}",
            CorrelationId = "corr-1",
            CreatedAt = FixedNow,
            CreatedBy = _user.UserId,
        });

        await ((ITenantModuleService)service).UpdateAsync(
            _tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false, "no shop")]);

        await using var verification = new PlatformDbContext(
            _options, new TestTenantProvider(_tenantId), _user, new FixedClock(FixedNow));
        var audits = await verification.AuditLogs
            .Where(log => log.Action == AuditEventNames.TenantModulesUpdated)
            .ToListAsync();

        audits.Should().ContainSingle("the abandoned attempt's audit row is detached before this attempt writes its own");
        audits.Single().DetailsJson.Should().NotContain("attempt", "the surviving record is the one this attempt wrote");
        (await LoadRowsAsync(_tenantId)).Should().ContainSingle(row => row.ModuleId == ModuleIds.Commerce);
    }

    [Fact]
    public async Task UpdateAsync_Should_StillInvalidateTheCache_When_AStepAfterTheCommitThrows()
    {
        IModuleEnablementReader reader = CreateService();
        (await reader.GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue("warm the cache first");
        var service = CreateAdminService(bus: new ThrowingEventBus());

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bus down");
        (await LoadRowsAsync(_tenantId)).Should().ContainSingle(row => row.ModuleId == ModuleIds.Commerce && !row.IsEnabled,
            "the change committed before the in-process publication ran");
        (await LoadOutboxAsync()).Should().ContainSingle();
        _audit.Entries.Should().ContainSingle();

        IModuleEnablementReader fresh = CreateService();
        (await fresh.GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeFalse(
            "a committed change is followed by invalidation no matter how the request ends, so this host never enforces stale state");
    }

    [Fact]
    public async Task UpdateAsync_Should_NotTouchAnotherTenant()
    {
        var otherTenant = Guid.NewGuid();
        SeedTenant(otherTenant);

        await CreateAdminService().UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false)]);

        (await LoadRowsAsync(otherTenant)).Should().BeEmpty();
        IModuleEnablementReader reader = CreateService(ambientTenantId: otherTenant);
        (await reader.GetAsync(otherTenant)).IsEnabled(ModuleIds.Commerce).Should().BeTrue();
    }

    // ── Fakes ───────────────────────────────────────────────────────────────────────────────────

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
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

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(new AuditEntry(action, resourceType, resourceId, tenantId, actorId, correlationId, detailsJson));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(
        string Action,
        string ResourceType,
        Guid ResourceId,
        Guid TenantId,
        Guid? ActorId,
        string? CorrelationId,
        string? DetailsJson);

    private sealed class ThrowingAuditLogWriter : IAuditLogWriter
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

    private sealed class ThrowingEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
            => throw new InvalidOperationException("bus down");
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

    /// <summary>
    /// Writes through the caller's context exactly as the production <c>AuditLogWriter</c> does, so a test
    /// can observe what the tracker actually holds. The recording fake above never touches the context.
    /// </summary>
    private sealed class ContextWritingAuditLogWriter(PlatformDbContext dbContext, DateTime now) : IAuditLogWriter
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
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Timestamp = now,
                ActorType = "User",
                ActorId = actorId ?? Guid.Empty,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                DetailsJson = detailsJson ?? string.Empty,
                CorrelationId = correlationId ?? string.Empty,
                CreatedAt = now,
                CreatedBy = actorId ?? Guid.Empty,
            });

            return dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
