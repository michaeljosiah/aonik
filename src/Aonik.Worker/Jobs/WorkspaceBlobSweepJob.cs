using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Workspaces.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Removes workspace blobs nothing references any more (Spec 089 §5.1).
///
/// <para>
/// Reference counting is what lets a revision be deleted without orphaning bytes another revision still names,
/// and the sweeper is what turns a count of zero into reclaimed storage. Without it, <c>RefCount</c> is a column
/// that describes an intention: every deleted revision leaves its blobs behind, and a world's takes are the
/// largest objects in the system.
/// </para>
///
/// <para>
/// It claims before it deletes, and abandons when the claim turns out to be stale — so the failure mode is one
/// redundant upload rather than a manifest pointing at bytes that are gone.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
internal sealed class WorkspaceBlobSweepJob : IJob
{
    public static readonly JobKey Key = new("WorkspaceBlobSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IWorkspaceBlobSweeper _sweeper;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WorkspaceBlobSweepJob> _logger;

    public WorkspaceBlobSweepJob(
        IWorkspaceBlobSweeper sweeper,
        ITenantContext tenantContext,
        ILogger<WorkspaceBlobSweepJob> logger)
    {
        _sweeper = sweeper;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var deleted = 0;
        var abandoned = 0;

        var tenants = await _sweeper.FindTenantsWithWorkAsync(context.CancellationToken);

        // Per tenant, because EnforceTenantOnWrites rejects a save whose rows belong to a tenant other
        // than the ambient one — a single pass over every tenant's blobs would fail on the first write.
        await TenantScopedJob.ForEachTenantAsync(
            _tenantContext, tenants, "workspace-blob-sweep",
            async ct =>
            {
                var summary = await _sweeper.SweepAsync(ct);

                deleted += summary.Deleted;
                abandoned += summary.Abandoned;

                return summary.Deleted;
            },
            _logger,
            context.CancellationToken);

        // Abandonment is reported rather than buried. It is the mechanism working — a reference landed
        // while the sweeper was mid-flight — but a sudden climb would mean something else, and a number
        // nobody can see is a number nobody will question.
        context.Result = $"Blobs deleted {deleted}, claims abandoned {abandoned}.";
    }
}
