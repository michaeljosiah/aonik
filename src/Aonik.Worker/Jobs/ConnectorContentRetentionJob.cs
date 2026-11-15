using Quartz;

using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Worker.Jobs;

[DisallowConcurrentExecution]
internal sealed class ConnectorContentRetentionJob(IConnectorContentRetention retention) : IJob
{
    public static readonly JobKey Key = new("ConnectorContentRetentionJob", ScheduledJobGroups.ScheduledJobs);

    public async Task Execute(IJobExecutionContext context)
    {
        int removed = await retention.PurgeExpiredAsync(context.CancellationToken);
        context.Result = $"Expired connector uploads removed {removed}.";
    }
}
