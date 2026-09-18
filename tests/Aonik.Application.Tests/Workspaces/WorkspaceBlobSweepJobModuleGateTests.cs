using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.Worker.Jobs;
using Aonik.Workspaces.Services;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Quartz;

namespace Aonik.Application.Tests.Workspaces;

/// <summary>
/// Spec 097 §12.2 / acceptance 11 for the Workspaces sweep: the job narrows the tenants that have
/// work through the reader for the Workspaces module, sweeps only those, and records the skip in
/// its execution result.
/// </summary>
public class WorkspaceBlobSweepJobModuleGateTests
{
    private static readonly Guid EnabledTenant = Guid.NewGuid();
    private static readonly Guid DisabledTenant = Guid.NewGuid();

    [Fact]
    public async Task Execute_Should_SweepOnlyEnabledTenants_AndRecordTheSkip_When_WorkspacesIsOffForATenant()
    {
        // Arrange
        var tenantContext = new FakeTenantContext();
        var sweptTenants = new List<Guid?>();
        var sweeper = new Mock<IWorkspaceBlobSweeper>();
        sweeper.Setup(s => s.FindTenantsWithWorkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        sweeper.Setup(s => s.SweepAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sweptTenants.Add(tenantContext.TenantId))
            .ReturnsAsync(new BlobSweepSummary(1, 0, 0));
        var job = new WorkspaceBlobSweepJob(
            sweeper.Object, tenantContext, NullLogger<WorkspaceBlobSweepJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        sweeper.Verify(s => s.SweepAsync(It.IsAny<CancellationToken>()), Times.Once);
        sweptTenants.Should().BeEquivalentTo(new Guid?[] { EnabledTenant });
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Blobs deleted 1")
            .And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Workspaces}' disabled");
    }

    [Fact]
    public async Task Execute_Should_SweepEveryTenant_When_NoReaderIsRegistered()
    {
        // Arrange
        var tenantContext = new FakeTenantContext();
        var sweeper = new Mock<IWorkspaceBlobSweeper>();
        sweeper.Setup(s => s.FindTenantsWithWorkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { EnabledTenant, DisabledTenant });
        sweeper.Setup(s => s.SweepAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobSweepSummary(1, 0, 0));
        var job = new WorkspaceBlobSweepJob(sweeper.Object, tenantContext, NullLogger<WorkspaceBlobSweepJob>.Instance);
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        sweeper.Verify(s => s.SweepAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        context.Result.Should().BeOfType<string>().Which.Should().Contain("Blobs deleted 2").And.NotContain("Skipped");
    }

    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(c => c.Result);
        return mock.Object;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    /// <summary>Every module on for every tenant except Workspaces for the given tenants.</summary>
    private sealed class FakeReader(params Guid[] workspacesOffFor) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (workspacesOffFor.Contains(tenantId))
                enabled.Remove(ModuleIds.Workspaces);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = moduleId == ModuleIds.Workspaces
                ? tenantIds.Distinct().Where(id => !workspacesOffFor.Contains(id)).ToList()
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
