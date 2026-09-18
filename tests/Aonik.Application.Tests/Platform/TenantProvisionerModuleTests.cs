using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Packs;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 097 §12.4: the provisioner writes the pack's module rows BEFORE the contributor loop and skips
/// every <see cref="ITenantProvisioningContributor"/> whose module resolved off for the tenant. Core
/// modules and names the catalogue does not know always run.
/// </summary>
public class TenantProvisionerModuleTests
{
    private static readonly Guid TenantId = Guid.Parse("e0000000-0000-0000-0000-000000000097");
    private static readonly Guid ActorId = Guid.Parse("e0000000-0000-0000-0000-0000000000bb");
    private static readonly DateTime Now = new(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);

    private delegate void TryGetTenantDelegate(out Guid tenantId);

    private sealed class Harness
    {
        public List<string> CallLog { get; } = [];
        public List<bool> InitialProvisioningFlags { get; } = [];
        public PlatformDbContext Db { get; }
        public Mock<IServiceProvider> ServiceProvider { get; } = new();
        public Mock<IConfigPackApplier> Applier { get; } = new();

        public Harness(int? appliedPackVersion = null, bool withExistingModuleRow = false)
        {
            var tenantProvider = new Mock<ITenantProvider>();
            tenantProvider.Setup(x => x.GetCurrentTenantId()).Returns(TenantId);
            tenantProvider
                .Setup(x => x.TryGetCurrentTenantId(out It.Ref<Guid>.IsAny))
                .Callback(new TryGetTenantDelegate((out Guid id) => id = TenantId))
                .Returns(true);

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.Setup(x => x.GetCurrentUserId()).Returns(ActorId);

            var clock = new Mock<IClock>();
            clock.SetupGet(x => x.UtcNow).Returns(Now);

            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;
            Db = new PlatformDbContext(options, tenantProvider.Object, currentUser.Object, clock.Object);
            Db.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme", DefaultCurrency = "GBP", Status = "Active", BusinessType = "food-commerce", AppliedPackVersion = appliedPackVersion });
            if (withExistingModuleRow)
            {
                Db.TenantModules.Add(new TenantModule
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ModuleId = ModuleIds.Commerce,
                    IsEnabled = true,
                    Source = TenantModuleSource.Pack,
                    Reason = "pack:food-commerce@v1",
                });
            }
            Db.SaveChanges();

            Applier
                .Setup(a => a.ApplyModulesAsync(TenantId, "food-commerce", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, string, bool, CancellationToken>((_, _, initial, _) =>
                {
                    CallLog.Add("modules");
                    InitialProvisioningFlags.Add(initial);
                })
                .ReturnsAsync(new[] { "Applied module set" });
            Applier
                .Setup(a => a.ApplyAsync(TenantId, "food-commerce", It.IsAny<CancellationToken>()))
                .Callback(() => CallLog.Add("pack"))
                .ReturnsAsync(ConfigPackResult.None);
            ServiceProvider.Setup(sp => sp.GetService(typeof(IConfigPackApplier))).Returns(Applier.Object);

            CurrentUser = currentUser.Object;
            Clock = clock.Object;
        }

        public ICurrentUserProvider CurrentUser { get; }
        public IClock Clock { get; }

        public void WithReader(params string[] enabled)
        {
            var reader = new FakeModuleEnablementReader(enabled);
            ServiceProvider.Setup(sp => sp.GetService(typeof(IModuleEnablementReader))).Returns(reader);
        }

        public RecordingContributor Contributor(string moduleName)
            => new(moduleName, CallLog);

        public TenantProvisioner Build(params ITenantProvisioningContributor[] contributors)
        {
            var correlation = new Mock<ICorrelationContext>();
            correlation.SetupGet(x => x.CorrelationId).Returns("corr-097");

            var permissionService = new Mock<IPermissionService>();
            permissionService
                .Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            return new TenantProvisioner(
                Db,
                new Mock<IAuditLogWriter>().Object,
                Clock,
                CurrentUser,
                correlation.Object,
                permissionService.Object,
                contributors,
                ServiceProvider.Object);
        }
    }

    private static string[] AllExcept(params string[] off)
        => ModuleCatalog.All.Select(descriptor => descriptor.Id).Except(off).ToArray();

    [Fact]
    public async Task ProvisionTenantAsync_Should_SkipContributor_When_ItsModuleIsDisabled()
    {
        var harness = new Harness();
        harness.WithReader(AllExcept(ModuleIds.Commerce));
        var commerce = harness.Contributor(ModuleIds.Commerce);
        var finance = harness.Contributor(ModuleIds.Finance);
        var ai = harness.Contributor(ModuleIds.Ai);
        var legacy = harness.Contributor("LegacyName");
        var provisioner = harness.Build(commerce, finance, ai, legacy);

        var result = await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        commerce.Provisioned.Should().BeFalse("commerce resolved off for this tenant");
        finance.Provisioned.Should().BeTrue("finance resolved on");
        ai.Provisioned.Should().BeTrue("ai is core and can never be off");
        legacy.Provisioned.Should().BeTrue("a name the catalogue does not know runs as before Spec 097");
        result.ActionsPerformed.Should().Contain("Skipped commerce provisioning: module disabled for tenant");
        result.ActionsPerformed.Should().Contain("Applied module set");
        result.ActionsPerformed.Should().Contain("Provisioned finance");
        result.ActionsPerformed.Should().NotContain("Provisioned commerce");
    }

    [Fact]
    public async Task ProvisionTenantAsync_Should_ApplyModuleRows_Before_ContributorsRun()
    {
        var harness = new Harness();
        harness.WithReader(AllExcept());
        var finance = harness.Contributor(ModuleIds.Finance);
        var provisioner = harness.Build(finance);

        await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        harness.CallLog.Should().ContainInOrder("modules", $"contributor:{ModuleIds.Finance}", "pack");
        harness.CallLog.IndexOf("modules").Should().BeLessThan(harness.CallLog.IndexOf($"contributor:{ModuleIds.Finance}"),
            "the module set must exist before any contributor is consulted");
    }

    [Fact]
    public async Task ProvisionTenantAsync_Should_ApplyPackAsInitialProvisioning_When_TenantHasNoRowsAndNoPackVersion()
    {
        var harness = new Harness();
        harness.WithReader(AllExcept());
        var provisioner = harness.Build();

        await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        harness.InitialProvisioningFlags.Should().ContainSingle().Which.Should().BeTrue(
            "a tenant with no module rows and no stamped pack version is being provisioned for the first time");
    }

    [Fact]
    public async Task ProvisionTenantAsync_Should_ApplyPackAdditively_When_TenantAlreadyHasAPackVersion()
    {
        // A pre-Spec-097 tenant: pack stamped, no rows. Re-provisioning must not narrow its module set.
        var harness = new Harness(appliedPackVersion: 1);
        harness.WithReader(AllExcept());
        var provisioner = harness.Build();

        await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        harness.InitialProvisioningFlags.Should().ContainSingle().Which.Should().BeFalse(
            "a re-run of provisioning on an existing tenant takes the additive path");
    }

    [Fact]
    public async Task ProvisionTenantAsync_Should_ApplyPackAdditively_When_TenantAlreadyHasModuleRows()
    {
        var harness = new Harness(withExistingModuleRow: true);
        harness.WithReader(AllExcept());
        var provisioner = harness.Build();

        await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        harness.InitialProvisioningFlags.Should().ContainSingle().Which.Should().BeFalse(
            "a tenant that already has module rows was provisioned before");
    }

    [Fact]
    public async Task ProvisionTenantAsync_Should_RunEveryContributor_When_NoModuleReaderIsRegistered()
    {
        var harness = new Harness(); // no WithReader: GetService(IModuleEnablementReader) returns null
        var commerce = harness.Contributor(ModuleIds.Commerce);
        var provisioner = harness.Build(commerce);

        var result = await ((IBootstrapTenantProvisioner)provisioner).ProvisionTenantAsync(TenantId, default);

        commerce.Provisioned.Should().BeTrue("a host without the module graph gets the pre-Spec-097 behaviour");
        result.ActionsPerformed.Should().NotContain(action => action.StartsWith("Skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckTenantHealthAsync_Should_SkipContributor_When_ItsModuleIsDisabled()
    {
        var harness = new Harness();
        harness.WithReader(AllExcept(ModuleIds.Commerce));
        var commerce = harness.Contributor(ModuleIds.Commerce);
        var finance = harness.Contributor(ModuleIds.Finance);
        var provisioner = harness.Build(commerce, finance);

        var health = await provisioner.CheckTenantHealthAsync(TenantId);

        commerce.HealthChecked.Should().BeFalse("a disabled module's health findings would be false positives");
        finance.HealthChecked.Should().BeTrue();
        health.Issues.Should().Contain($"health:{ModuleIds.Finance}");
        health.Issues.Should().NotContain($"health:{ModuleIds.Commerce}");
    }

    [Theory]
    [InlineData(ModuleIds.Commerce, false, true)]
    [InlineData(ModuleIds.Commerce, true, false)]
    [InlineData(ModuleIds.Ai, false, false)]
    [InlineData("LegacyName", false, false)]
    public void IsModuleDisabled_Should_SkipOnlyKnownNonCoreDisabledModules(string moduleName, bool enabledInSet, bool expectedSkip)
    {
        var enabled = enabledInSet ? AllExcept() : AllExcept(moduleName);
        var set = new ModuleEnablementSet(TenantId, enabled.ToHashSet(StringComparer.Ordinal));

        TenantProvisioner.IsModuleDisabled(moduleName, set).Should().Be(expectedSkip);
    }

    [Fact]
    public void IsModuleDisabled_Should_ReturnFalse_When_NoModuleSetIsAvailable()
    {
        TenantProvisioner.IsModuleDisabled(ModuleIds.Commerce, null).Should().BeFalse();
    }

    // ── fakes ───────────────────────────────────────────────────────────

    private sealed class RecordingContributor(string moduleName, List<string> callLog) : ITenantProvisioningContributor
    {
        public string ModuleName => moduleName;
        public bool Provisioned { get; private set; }
        public bool HealthChecked { get; private set; }

        public Task<TenantProvisioningContribution> ContributeProvisioningAsync(TenantProvisioningContext context, CancellationToken cancellationToken = default)
        {
            Provisioned = true;
            callLog.Add($"contributor:{moduleName}");
            return Task.FromResult(new TenantProvisioningContribution([$"Provisioned {moduleName}"]));
        }

        public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
        {
            HealthChecked = true;
            issues.Add($"health:{moduleName}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModuleEnablementReader(IEnumerable<string> enabled) : IModuleEnablementReader
    {
        private readonly HashSet<string> _enabled = enabled.ToHashSet(StringComparer.Ordinal);

        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ModuleEnablementSet(tenantId, _enabled));

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(_enabled.Contains(moduleId) ? tenantIds.Distinct().ToList() : []);
    }
}
