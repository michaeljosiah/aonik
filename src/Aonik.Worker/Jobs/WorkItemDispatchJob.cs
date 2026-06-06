using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Platform.Entities.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// The single once-a-minute heartbeat that drives Spec 034 task scheduling. It owns no
/// per-task triggers; it asks <see cref="IWorkItemDispatcher"/> to claim and fire whatever
/// <c>WorkItem</c> rows are due, across all tenants. Clustering-safe via the dispatcher's
/// row lease plus the unique-per-occurrence run row, so multiple Worker instances can run
/// it without double-firing.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class WorkItemDispatchJob : IJob
{
    public static readonly JobKey Key = new("WorkItemDispatchJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IWorkItemDispatcher _dispatcher;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<WorkItemDispatchJob> _logger;

    public WorkItemDispatchJob(
        IWorkItemDispatcher dispatcher,
        IOptions<ScheduledJobOptions> options,
        ILogger<WorkItemDispatchJob> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var settings = _options.WorkItemDispatch;
        if (!settings.Enabled)
        {
            context.Result = "Work item dispatch disabled.";
            return;
        }

        // The dispatcher manages tenant context internally — it scans across tenants under a
        // system context and processes each item under its own tenant.
        var summary = await _dispatcher.DispatchDueAsync(
            new WorkItemDispatchOptions(
                BatchSize: settings.BatchSize,
                LeaseSeconds: settings.LeaseSeconds,
                MaxAttempts: settings.MaxAttempts),
            context.CancellationToken);

        if (summary.Considered > 0)
        {
            _logger.LogInformation(
                "Work item dispatch: considered {Considered}, succeeded {Succeeded}, proposed {Proposed}, skipped {Skipped}, failed {Failed}.",
                summary.Considered, summary.Succeeded, summary.Proposed, summary.Skipped, summary.Failed);
        }

        context.Result =
            $"Considered {summary.Considered}, succeeded {summary.Succeeded}, proposed {summary.Proposed}, " +
            $"skipped {summary.Skipped}, failed {summary.Failed}.";
    }
}
