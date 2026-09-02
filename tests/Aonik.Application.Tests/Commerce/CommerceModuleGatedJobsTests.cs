using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Modules;
using Aonik.Worker.Jobs;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Quartz;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 097 §12.2 / acceptance 11 for the three Commerce sweeps: each job asks its service which
/// tenants have work, narrows that list through <see cref="IModuleEnablementReader"/> for the
/// Commerce module, hands only the enabled tenants back to the service, and records the skip in
/// its execution result. Without a reader (hosts and tests that build the job directly) every
/// tenant is swept, as before.
/// </summary>
public class CommerceModuleGatedJobsTests
{
    private static readonly Guid EnabledTenant = Guid.NewGuid();
    private static readonly Guid DisabledTenant = Guid.NewGuid();

    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(c => c.Result);
        return mock.Object;
    }

    private static IOptions<ScheduledJobOptions> JobOptions() => Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions());

    // ── Low-stock scan ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LowStockScan_Should_ScanOnlyEnabledTenants_AndRecordTheSkip_When_CommerceIsOffForATenant()
    {
        // Arrange
        var alerts = new Mock<ILowStockAlertService>();
        alerts.Setup(a => a.FindTenantsWithLowStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        IReadOnlyCollection<Guid>? scanned = null;
        alerts.Setup(a => a.ScanAndRaiseAsync(It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>?, CancellationToken>((ids, _) => scanned = ids)
            .ReturnsAsync(new LowStockScanResult(1, 0));
        var job = new LowStockScanJob(alerts.Object, JobOptions(), NullLogger<LowStockScanJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        scanned.Should().BeEquivalentTo(new[] { EnabledTenant });
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Raised 1").And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Commerce}' disabled");
    }

    [Fact]
    public async Task LowStockScan_Should_NotCallTheService_When_EveryTenantHasCommerceOff()
    {
        // Arrange
        var alerts = new Mock<ILowStockAlertService>();
        alerts.Setup(a => a.FindTenantsWithLowStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { DisabledTenant });
        var job = new LowStockScanJob(alerts.Object, JobOptions(), NullLogger<LowStockScanJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        alerts.Verify(a => a.ScanAndRaiseAsync(It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Result.Should().BeOfType<string>().Which.Should().Contain("Raised 0").And.Contain("Skipped 1 tenant(s)");
    }

    [Fact]
    public async Task LowStockScan_Should_ScanEveryTenant_When_NoReaderIsRegistered()
    {
        // Arrange
        var alerts = new Mock<ILowStockAlertService>();
        alerts.Setup(a => a.FindTenantsWithLowStockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        IReadOnlyCollection<Guid>? scanned = null;
        alerts.Setup(a => a.ScanAndRaiseAsync(It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>?, CancellationToken>((ids, _) => scanned = ids)
            .ReturnsAsync(new LowStockScanResult(2, 0));
        var job = new LowStockScanJob(alerts.Object, JobOptions(), NullLogger<LowStockScanJob>.Instance);
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        scanned.Should().BeEquivalentTo(new[] { EnabledTenant, DisabledTenant });
        context.Result.Should().BeOfType<string>().Which.Should().Contain("Raised 2").And.NotContain("Skipped");
    }

    // ── Inventory reservation sweep ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InventoryReservationSweep_Should_SweepOnlyEnabledTenants_AndRecordTheSkip()
    {
        // Arrange
        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.FindTenantsWithExpiredReservationsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        IReadOnlyCollection<Guid>? swept = null;
        inventory.Setup(i => i.ReleaseExpiredAsync(It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, IReadOnlyCollection<Guid>?, CancellationToken>((_, ids, _) => swept = ids)
            .ReturnsAsync(3);
        var job = new InventoryReservationSweepJob(
            inventory.Object, JobOptions(), NullLogger<InventoryReservationSweepJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        swept.Should().BeEquivalentTo(new[] { EnabledTenant });
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Released 3").And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Commerce}' disabled");
    }

    // ── Box cart abandon sweep ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BoxCartAbandonSweep_Should_SweepOnlyEnabledTenants_AndRecordTheSkip()
    {
        // Arrange
        var maintenance = new Mock<ICartMaintenanceService>();
        maintenance.Setup(m => m.FindTenantsWithIdleBoxCartsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        IReadOnlyCollection<Guid>? swept = null;
        maintenance.Setup(m => m.AbandonIdleBoxCartsAsync(It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, IReadOnlyCollection<Guid>?, CancellationToken>((_, ids, _) => swept = ids)
            .ReturnsAsync(2);
        var job = new BoxCartAbandonSweepJob(
            maintenance.Object, JobOptions(), NullLogger<BoxCartAbandonSweepJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        swept.Should().BeEquivalentTo(new[] { EnabledTenant });
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Abandoned 2").And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Commerce}' disabled");
    }

    /// <summary>Every module is on for every tenant except Commerce for the given tenants.</summary>
    private sealed class FakeReader(params Guid[] commerceOffFor) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (commerceOffFor.Contains(tenantId))
                enabled.Remove(ModuleIds.Commerce);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = moduleId == ModuleIds.Commerce
                ? tenantIds.Distinct().Where(id => !commerceOffFor.Contains(id)).ToList()
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
