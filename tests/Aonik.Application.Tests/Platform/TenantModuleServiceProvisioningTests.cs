using System.Text.Json;

using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Provisioning;
using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Codex P1-1 on Spec 097 §9: a module that transitions from off to on in the resolved set is
/// provisioned — its <see cref="ITenantProvisioningContributor"/>s run, dependencies first — BEFORE the
/// toggle is persisted, so a tenant whose pack disabled Finance at provisioning gets a ledger the moment
/// Finance is switched on, and a contributor failure leaves the module state untouched and audited.
/// </summary>
public class TenantModuleServiceProvisioningTests
{
    private static readonly DateTime FixedNow = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
    private const string CommerceAccountCode = "2300-COMMERCE-CLEARING";

    private readonly DbContextOptions<PlatformDbContext> _platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    private readonly DbContextOptions<FinanceDbContext> _financeOptions = new DbContextOptionsBuilder<FinanceDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    private readonly IFusionCache _cache = new FusionCache(new FusionCacheOptions());
    private readonly RecordingAuditLogWriter _audit = new();
    private readonly RecordingEventBus _bus = new();
    private readonly TestCurrentUserProvider _user = new();
    private readonly List<string> _callLog = [];
    private readonly Guid _tenantId = Guid.NewGuid();

    /// <summary>
    /// The scoped tenant context the service switches to the target tenant. The Finance context's tenant
    /// provider reads it, exactly as <c>HttpContextTenantProvider</c> does in the API, so the tests can
    /// prove the switch happens before any contributor runs.
    /// </summary>
    private readonly FakeTenantContext _tenantContext = new();

    public TenantModuleServiceProvisioningTests()
    {
        using var context = new PlatformDbContext(_platformOptions, new TestTenantProvider(_tenantId), _user);
        context.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Module Tenant",
            Environment = "Development",
            DefaultCurrency = "GBP",
            BusinessType = "food-commerce",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        context.SaveChanges();

        _tenantContext.TenantId = _tenantId;
        _tenantContext.ResolutionSource = "Test";
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────

    private TenantModuleService CreateService(params ITenantProvisioningContributor[] contributors)
        => CreateService(BuildProvider(contributors));

    private TenantModuleService CreateService(IServiceProvider? serviceProvider)
        => new(
            new PlatformDbContext(_platformOptions, new ContextBackedTenantProvider(_tenantContext), _user, new FixedClock(FixedNow)),
            _cache,
            NullLogger<TenantModuleService>.Instance,
            new FixedClock(FixedNow),
            _user,
            new FixedCorrelationContext("corr-097"),
            _audit,
            _bus,
            _tenantContext,
            new AllowAllPermissionService(),
            serviceProvider);

    private static IServiceProvider BuildProvider(IEnumerable<ITenantProvisioningContributor> contributors)
        => BuildProvider(contributors, []);

    private static IServiceProvider BuildProvider(
        IEnumerable<ITenantProvisioningContributor> contributors,
        IEnumerable<ILedgerAccountContributor> accountContributors)
    {
        var services = new ServiceCollection();
        foreach (var contributor in contributors)
            services.AddSingleton(contributor);
        foreach (var accountContributor in accountContributors)
            services.AddSingleton(accountContributor);
        return services.BuildServiceProvider();
    }

    /// <summary>A Finance context bound to the SAME scoped tenant context the service switches.</summary>
    private FinanceDbContext CreateFinanceDb()
        => new(_financeOptions, new ContextBackedTenantProvider(_tenantContext), _user, new FixedClock(FixedNow));

    /// <summary>A Finance context pinned to <paramref name="tenantId"/>, for seeding and assertions.</summary>
    private FinanceDbContext CreateFinanceDb(Guid tenantId)
        => new(_financeOptions, new TestTenantProvider(tenantId), _user, new FixedClock(FixedNow));

    private async Task SeedRowAsync(string moduleId, bool isEnabled)
    {
        await using var context = new PlatformDbContext(_platformOptions, new TestTenantProvider(_tenantId), _user, new FixedClock(FixedNow.AddDays(-1)));
        context.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = TenantModuleSource.Pack,
            CreatedAt = FixedNow.AddDays(-1),
        });
        await context.SaveChangesAsync();
    }

    /// <summary>The shape a declaring pack leaves behind: Finance and everything that hard-depends on it off.</summary>
    private async Task SeedFinanceChainOffAsync()
    {
        await SeedRowAsync(ModuleIds.Finance, isEnabled: false);
        await SeedRowAsync(ModuleIds.Commerce, isEnabled: false);
        await SeedRowAsync(ModuleIds.Subscriptions, isEnabled: false);
        await SeedRowAsync(ModuleIds.Workspaces, isEnabled: false);
    }

    private async Task<List<TenantModule>> LoadRowsAsync()
    {
        await using var context = new PlatformDbContext(_platformOptions, new TestTenantProvider(_tenantId), _user);
        return await context.TenantModules.Where(row => row.TenantId == _tenantId).ToListAsync();
    }

    private async Task<List<Aonik.SharedKernel.Events.Outbox.OutboxMessage>> LoadOutboxAsync()
    {
        await using var context = new PlatformDbContext(_platformOptions, new TestTenantProvider(_tenantId), _user);
        return await context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().ToListAsync();
    }

    private RecordingContributor Contributor(string moduleName) => new(moduleName, _callLog);

    // ── provisioning on enable ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_RunTheLedgerSeamOwner_When_TheEnabledModuleContributesAccounts()
    {
        // Subscriptions supplies its 2210/4100/4110/5100 codes through an ILedgerAccountContributor,
        // and that seam is walked by FINANCE's provisioning contributor. Enabling Subscriptions on a
        // tenant that already has Finance would otherwise report success with none of those accounts
        // created, and the first usage posting would fail on a missing code.
        await SeedRowAsync(ModuleIds.Subscriptions, isEnabled: false);
        var subscriptions = Contributor(ModuleIds.Subscriptions);
        var finance = Contributor(ModuleIds.Finance);
        ITenantModuleService service = CreateService(BuildProvider(
            [subscriptions, finance],
            [new FakeAccountContributor(ModuleIds.Subscriptions, "2210")]));

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Subscriptions, true, "metered billing")]);

        subscriptions.Calls.Should().Be(1);
        finance.Calls.Should().Be(1, "the owner of the ledger-account seam runs so the new module's accounts exist");
        _callLog.Should().Equal($"contributor:{ModuleIds.Subscriptions}", $"contributor:{ModuleIds.Finance}");
    }

    [Fact]
    public async Task UpdateAsync_Should_NotRunTheLedgerSeamOwnerTwice_When_ItIsItselfComingOn()
    {
        await SeedFinanceChainOffAsync();
        var subscriptions = Contributor(ModuleIds.Subscriptions);
        var finance = Contributor(ModuleIds.Finance);
        ITenantModuleService service = CreateService(BuildProvider(
            [subscriptions, finance],
            [new FakeAccountContributor(ModuleIds.Subscriptions, "2210")]));

        await service.UpdateAsync(_tenantId, [
            new TenantModuleToggle(ModuleIds.Finance, true, "payments"),
            new TenantModuleToggle(ModuleIds.Subscriptions, true, "metered billing"),
        ]);

        finance.Calls.Should().Be(1, "it already ran as a module coming on; the seam must not add a second pass");
        subscriptions.Calls.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Should_NotRunTheLedgerSeamOwner_When_NoEnabledModuleContributesAccounts()
    {
        await SeedRowAsync(ModuleIds.Groups, isEnabled: false);
        var groups = Contributor(ModuleIds.Groups);
        var finance = Contributor(ModuleIds.Finance);
        ITenantModuleService service = CreateService(BuildProvider(
            [groups, finance],
            [new FakeAccountContributor(ModuleIds.Subscriptions, "2210")]));

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Groups, true, "circles")]);

        groups.Calls.Should().Be(1);
        finance.Calls.Should().Be(0, "groups contributes no ledger accounts, so the seam owner has nothing to add");
    }

    [Fact]
    public async Task UpdateAsync_Should_RunTheFinanceContributorExactlyOnce_When_EnablingFinanceThatWasOff()
    {
        await SeedFinanceChainOffAsync();
        var finance = Contributor(ModuleIds.Finance);
        ITenantModuleService service = CreateService(finance);

        var list = await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true, "customer signed up for payments")]);

        finance.Calls.Should().Be(1);
        finance.LastContext.Should().NotBeNull();
        finance.LastContext!.TenantId.Should().Be(_tenantId);
        finance.LastContext.DefaultCurrency.Should().Be("GBP", "the context is built from the tenant row");
        finance.LastContext.BusinessType.Should().Be("food-commerce");
        finance.LastContext.UserId.Should().Be(_user.UserId);
        finance.LastContext.Now.Should().Be(FixedNow);

        list.Modules.Single(module => module.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeTrue();
        (await LoadRowsAsync()).Single(row => row.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeTrue("the toggle persisted after provisioning");
        _bus.Published.Should().ContainSingle().Which.Should().BeOfType<TenantModulesChangedEvent>()
            .Which.Enabled.Should().Equal(ModuleIds.Finance);

        var entry = _audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be(AuditEventNames.TenantModulesUpdated);
        using var payload = JsonDocument.Parse(entry.DetailsJson!);
        var provisioned = payload.RootElement.GetProperty("provisioned").EnumerateArray().Single();
        provisioned.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Finance);
        provisioned.GetProperty("contributor").GetString().Should().Be(nameof(RecordingContributor));
        provisioned.GetProperty("actions").EnumerateArray().Select(action => action.GetString())
            .Should().Equal($"Provisioned {ModuleIds.Finance}");
    }

    [Fact]
    public async Task UpdateAsync_Should_NotPersistTheToggle_When_AContributorThrows()
    {
        await SeedFinanceChainOffAsync();
        IModuleEnablementReader reader = CreateService(serviceProvider: null);
        (await reader.GetAsync(_tenantId)).IsEnabled(ModuleIds.Finance).Should().BeFalse("warm the cache first");
        var rowsBefore = await LoadRowsAsync();
        ITenantModuleService service = CreateService(new ThrowingContributor(ModuleIds.Finance));

        var act = () => service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true, "try payments")]);

        var thrown = await act.Should().ThrowAsync<ModuleProvisioningException>();
        thrown.Which.ModuleId.Should().Be(ModuleIds.Finance);
        thrown.Which.Contributor.Should().Be(nameof(ThrowingContributor));
        thrown.Which.Code.Should().Be(ModuleProvisioningException.ProvisioningFailed);
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("ledger store unavailable");

        var rowsAfter = await LoadRowsAsync();
        rowsAfter.Should().HaveCount(rowsBefore.Count, "no row is created on a failed enablement");
        rowsAfter.Single(row => row.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeFalse("the toggle is not persisted");
        rowsAfter.Single(row => row.ModuleId == ModuleIds.Finance).UpdatedAt.Should().BeNull("the row was never touched");
        rowsAfter.Single(row => row.ModuleId == ModuleIds.Finance).Source.Should().Be(TenantModuleSource.Pack);
        (await LoadOutboxAsync()).Should().BeEmpty("no change means no event on the outbox");
        _bus.Published.Should().BeEmpty();

        var entry = _audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be(TenantModuleService.ProvisioningFailedAuditAction);
        entry.ResourceType.Should().Be("TenantModules");
        entry.TenantId.Should().Be(_tenantId);
        using var payload = JsonDocument.Parse(entry.DetailsJson!);
        payload.RootElement.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Finance);
        payload.RootElement.GetProperty("contributor").GetString().Should().Be(nameof(ThrowingContributor));
        payload.RootElement.GetProperty("requested").EnumerateArray().Select(element => element.GetString()).Should().Equal(ModuleIds.Finance);
        payload.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("ledger store unavailable");
        payload.RootElement.GetProperty("error").GetProperty("type").GetString().Should().Be(typeof(InvalidOperationException).FullName);

        var cached = await _cache.TryGetAsync<IReadOnlySet<string>>(TenantModuleService.CacheKey(_tenantId));
        cached.HasValue.Should().BeTrue("nothing committed, so nothing is invalidated");
        (await reader.GetAsync(_tenantId)).IsEnabled(ModuleIds.Finance).Should().BeFalse("the pre-seeded memo must not leak the intended set after a failure");
    }

    [Fact]
    public async Task UpdateAsync_Should_ProvisionDependenciesFirst_When_AModuleAndItsDependencyComeOnInTheSameRequest()
    {
        await SeedFinanceChainOffAsync();
        ITenantModuleService service = CreateService(Contributor(ModuleIds.Commerce), Contributor(ModuleIds.Finance));

        // Commerce is listed first on purpose: the order is the dependency graph's, not the request's.
        await service.UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Commerce, true),
            new TenantModuleToggle(ModuleIds.Finance, true),
        ]);

        _callLog.Should().Equal($"contributor:{ModuleIds.Finance}", $"contributor:{ModuleIds.Commerce}");
        var rows = await LoadRowsAsync();
        rows.Single(row => row.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeTrue();
        rows.Single(row => row.ModuleId == ModuleIds.Commerce).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_Should_ProvisionAModuleTheCascadeSwitchesOn_When_ItsDependencyIsEnabled()
    {
        // Only Finance has a row; Commerce, Subscriptions and Workspaces default on but resolve off
        // through the closure. Enabling Finance switches all of them on without being in the request.
        await SeedRowAsync(ModuleIds.Finance, isEnabled: false);
        var commerce = Contributor(ModuleIds.Commerce);
        var finance = Contributor(ModuleIds.Finance);
        ITenantModuleService service = CreateService(commerce, finance);

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        _callLog.Should().Equal([$"contributor:{ModuleIds.Finance}", $"contributor:{ModuleIds.Commerce}"],
            "a dependency that comes on as part of the cascade is provisioned too, after what it depends on");
        _bus.Published.Should().ContainSingle().Which.Should().BeOfType<TenantModulesChangedEvent>()
            .Which.Enabled.Should().Equal(ModuleIds.Commerce, ModuleIds.Finance, ModuleIds.Subscriptions, ModuleIds.Workspaces);
    }

    [Fact]
    public async Task UpdateAsync_Should_RunNoContributor_When_TogglingOff()
    {
        var commerce = Contributor(ModuleIds.Commerce);
        ITenantModuleService service = CreateService(commerce);

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Commerce, false, "no shop")]);

        commerce.Calls.Should().Be(0, "disabling never provisions");
        _callLog.Should().BeEmpty();
        using var payload = JsonDocument.Parse(_audit.Entries.Single().DetailsJson!);
        payload.RootElement.GetProperty("provisioned").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_Should_RunNoContributor_When_TheModuleWasAlreadyOn()
    {
        var finance = Contributor(ModuleIds.Finance);
        var legacy = Contributor("LegacyName");
        ITenantModuleService service = CreateService(finance, legacy);

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true, "make it explicit")]);

        finance.Calls.Should().Be(0, "finance resolved on before and after, so nothing transitioned");
        legacy.Calls.Should().Be(0, "a contributor whose name is not a catalogue module never maps to a transition");
        (await LoadRowsAsync()).Single(row => row.ModuleId == ModuleIds.Finance).Source.Should().Be(TenantModuleSource.Explicit);
    }

    [Fact]
    public async Task UpdateAsync_Should_LeaveProvisionedDataUntouched_When_ReEnablingAnAlreadyProvisionedModule()
    {
        // A tenant provisioned WITH Finance, later switched off, now switched back on: the real Finance
        // contributor runs again and must find everything already there.
        await using (var seed = CreateFinanceDb(_tenantId))
        {
            await new FinanceTenantProvisioningContributor(seed)
                .ContributeProvisioningAsync(new TenantProvisioningContext(_tenantId, "GBP", _user.UserId, FixedNow.AddDays(-2)));
        }
        await SeedFinanceChainOffAsync();

        await using var financeDb = CreateFinanceDb();
        ITenantModuleService service = CreateService(new FinanceTenantProvisioningContributor(financeDb));

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        await using var verify = CreateFinanceDb(_tenantId);
        (await verify.Ledgers.CountAsync(ledger => ledger.TenantId == _tenantId)).Should().Be(1, "no second ledger");
        (await verify.LedgerAccounts.CountAsync(account => account.TenantId == _tenantId)).Should().Be(7, "the default chart is not duplicated");
        (await verify.FeePolicies.CountAsync(policy => policy.TenantId == _tenantId)).Should().Be(1);
        (await verify.LimitsPolicies.CountAsync(policy => policy.TenantId == _tenantId)).Should().Be(1);

        using var payload = JsonDocument.Parse(_audit.Entries.Single().DetailsJson!);
        var actions = payload.RootElement.GetProperty("provisioned").EnumerateArray().Single()
            .GetProperty("actions").EnumerateArray().Select(action => action.GetString()).ToList();
        actions.Should().Contain("Ledger already exists - skipped");
        actions.Should().Contain("Fee policies already exist - skipped");
        actions.Should().Contain("Limits policies already exist - skipped");
    }

    [Fact]
    public async Task UpdateAsync_Should_CreateTheLedger_When_EnablingFinanceForATenantProvisionedWithoutIt()
    {
        await SeedFinanceChainOffAsync();
        await using var financeDb = CreateFinanceDb();
        ITenantModuleService service = CreateService(new FinanceTenantProvisioningContributor(financeDb));

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        await using var verify = CreateFinanceDb(_tenantId);
        var ledger = (await verify.Ledgers.Where(ledger => ledger.TenantId == _tenantId).ToListAsync()).Should().ContainSingle().Which;
        ledger.BaseCurrency.Should().Be("GBP", "the tenant's default currency drives the ledger");
        (await verify.LedgerAccounts.CountAsync(account => account.TenantId == _tenantId)).Should().Be(7,
            "the posting paths resolve accounts by code, so the chart must exist before Finance is reported enabled");
    }

    [Fact]
    public async Task UpdateAsync_Should_LetContributorsSeeThePostToggleSet_When_ProvisioningADependencyAndItsDependentTogether()
    {
        // Finance's contributor gates other modules' ledger accounts on the module reader — this very
        // service. While provisioning it must see the set the request is about to commit (Commerce on),
        // not the stored one (Commerce off), or Commerce would come on without its clearing account.
        await SeedFinanceChainOffAsync();
        await using var financeDb = CreateFinanceDb();
        var service = CreateReaderBoundService(reader => new FinanceTenantProvisioningContributor(
            financeDb,
            [new FakeAccountContributor(ModuleIds.Commerce, CommerceAccountCode)],
            moduleReader: reader));

        await ((ITenantModuleService)service).UpdateAsync(_tenantId,
        [
            new TenantModuleToggle(ModuleIds.Finance, true),
            new TenantModuleToggle(ModuleIds.Commerce, true),
        ]);

        await using var verify = CreateFinanceDb(_tenantId);
        (await verify.LedgerAccounts.AnyAsync(account => account.TenantId == _tenantId && account.Code == CommerceAccountCode))
            .Should().BeTrue("Commerce is on in the set being committed, so its accounts are created");
        (await ((IModuleEnablementReader)service).GetAsync(_tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_Should_NotCreateAccountsForAModuleThatStaysOff_When_ProvisioningItsDependency()
    {
        await SeedFinanceChainOffAsync();
        await using var financeDb = CreateFinanceDb();
        var service = CreateReaderBoundService(reader => new FinanceTenantProvisioningContributor(
            financeDb,
            [new FakeAccountContributor(ModuleIds.Commerce, CommerceAccountCode)],
            moduleReader: reader));

        await ((ITenantModuleService)service).UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        await using var verify = CreateFinanceDb(_tenantId);
        (await verify.LedgerAccounts.AnyAsync(account => account.TenantId == _tenantId && account.Code == CommerceAccountCode))
            .Should().BeFalse("Commerce stays off, so the reader the contributor consults still says off");
        (await verify.Ledgers.CountAsync(ledger => ledger.TenantId == _tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Should_SwitchTheAmbientTenantToTheTarget_BeforeContributorsRun()
    {
        // A host admin acting from their OWN tenant: the Finance context filters by the ambient tenant,
        // so if the switch happened after the contributor the "ledger already exists" check would look
        // at the wrong tenant and create a second ledger.
        await using (var seed = CreateFinanceDb(_tenantId))
        {
            await new FinanceTenantProvisioningContributor(seed)
                .ContributeProvisioningAsync(new TenantProvisioningContext(_tenantId, "GBP", _user.UserId, FixedNow.AddDays(-2)));
        }
        await SeedFinanceChainOffAsync();
        var hostTenant = Guid.NewGuid();
        _tenantContext.TenantId = hostTenant;
        _tenantContext.ResolutionSource = "Header";

        await using var financeDb = CreateFinanceDb();
        var tenantSeenByContributor = new List<Guid?>();
        var probe = new ProbeContributor(ModuleIds.Finance, () => tenantSeenByContributor.Add(_tenantContext.TenantId));
        ITenantModuleService service = CreateService(probe, new FinanceTenantProvisioningContributor(financeDb));

        await service.UpdateAsync(_tenantId, [new TenantModuleToggle(ModuleIds.Finance, true)]);

        tenantSeenByContributor.Should().Equal([_tenantId], "the ambient tenant is the target tenant while contributors run");
        _tenantContext.ResolutionSource.Should().Be("AdminTenantAction");
        await using var verify = CreateFinanceDb(_tenantId);
        (await verify.Ledgers.CountAsync(ledger => ledger.TenantId == _tenantId)).Should().Be(1, "the existing ledger was found under the target tenant, not duplicated");
        (await verify.Ledgers.CountAsync(ledger => ledger.TenantId == hostTenant)).Should().Be(0, "nothing was written under the host admin's tenant");
    }

    [Fact]
    public void OrderByHardDependencies_Should_PlaceEveryDependencyBeforeItsDependents()
    {
        var ordered = TenantModuleService.OrderByHardDependencies(
            [ModuleIds.Workspaces, ModuleIds.Commerce, ModuleIds.Finance, ModuleIds.Groups, ModuleIds.Subscriptions]).ToList();

        ordered.Should().BeEquivalentTo([ModuleIds.Workspaces, ModuleIds.Commerce, ModuleIds.Finance, ModuleIds.Groups, ModuleIds.Subscriptions]);
        foreach (var moduleId in ordered)
        {
            foreach (var dependency in ModuleCatalog.Get(moduleId).DependsOn.Where(ordered.Contains))
                ordered.IndexOf(dependency).Should().BeLessThan(ordered.IndexOf(moduleId), $"{dependency} must be provisioned before {moduleId}");
        }
    }

    /// <summary>
    /// What a request scope gives for free: the SAME instance is the <see cref="IModuleEnablementReader"/>
    /// the contributor consults and the writer that performs the toggle. The service resolves
    /// contributors lazily (that is what breaks the DI cycle in production), so a factory registration
    /// that closes over the not-yet-built service is resolved only inside UpdateAsync, by which time it
    /// is assigned — the same order the container observes.
    /// </summary>
    private TenantModuleService CreateReaderBoundService(Func<IModuleEnablementReader, ITenantProvisioningContributor> contributorFactory)
    {
        TenantModuleService? service = null;
        var provider = new ServiceCollection()
            .AddSingleton<ITenantProvisioningContributor>(_ => contributorFactory(service!))
            .BuildServiceProvider();
        service = CreateService(provider);
        return service;
    }

    // ── fakes ───────────────────────────────────────────────────────────────────────────────────

    private sealed class RecordingContributor(string moduleName, List<string> callLog) : ITenantProvisioningContributor
    {
        public string ModuleName => moduleName;
        public int Calls { get; private set; }
        public TenantProvisioningContext? LastContext { get; private set; }

        public Task<TenantProvisioningContribution> ContributeProvisioningAsync(TenantProvisioningContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastContext = context;
            callLog.Add($"contributor:{moduleName}");
            return Task.FromResult(new TenantProvisioningContribution([$"Provisioned {moduleName}"]));
        }

        public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingContributor(string moduleName) : ITenantProvisioningContributor
    {
        public string ModuleName => moduleName;

        public Task<TenantProvisioningContribution> ContributeProvisioningAsync(TenantProvisioningContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("ledger store unavailable");

        public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ProbeContributor(string moduleName, Action onProvision) : ITenantProvisioningContributor
    {
        public string ModuleName => moduleName;

        public Task<TenantProvisioningContribution> ContributeProvisioningAsync(TenantProvisioningContext context, CancellationToken cancellationToken = default)
        {
            onProvision();
            return Task.FromResult(new TenantProvisioningContribution(["probed"]));
        }

        public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAccountContributor(string moduleName, string code) : ILedgerAccountContributor
    {
        public string ModuleName => moduleName;

        public IReadOnlyCollection<LedgerAccountDefinition> GetAccounts()
            => [new LedgerAccountDefinition(code, $"Account {code}", "Liability")];
    }

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

    /// <summary>Mirrors <c>HttpContextTenantProvider</c>: the provider is a view over the scoped tenant context.</summary>
    private sealed class ContextBackedTenantProvider(ITenantContext tenantContext) : ITenantProvider
    {
        public Guid GetCurrentTenantId()
            => tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context not available");

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            if (tenantContext.IsResolved && tenantContext.TenantId.HasValue)
            {
                tenantId = tenantContext.TenantId.Value;
                return true;
            }

            tenantId = Guid.Empty;
            return false;
        }
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
