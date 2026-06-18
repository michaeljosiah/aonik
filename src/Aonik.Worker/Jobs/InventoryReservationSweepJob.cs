using Aonik.Commerce.Services.Inventory;
using Aonik.Platform.Entities.Operations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Releases expired held inventory reservations (Spec 042 §10) so abandoned checkouts free stock.
/// Sweeps across all tenants under a system context; clustering-safe via Quartz job storage and the
/// idempotent status transition (only <c>Held</c> rows are released).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class InventoryReservationSweepJob : IJob
{
    public static readonly JobKey Key = new("InventoryReservationSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IInventoryService _inventory;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<InventoryReservationSweepJob> _logger;

    public InventoryReservationSweepJob(
        IInventoryService inventory,
        IOptions<ScheduledJobOptions> options,
        ILogger<InventoryReservationSweepJob> logger)
    {
        _inventory = inventory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.InventoryReservationSweep.Enabled)
        {
            context.Result = "Inventory reservation sweep disabled.";
            return;
        }

        var released = await _inventory.ReleaseExpiredAsync(cancellationToken: context.CancellationToken);
        context.Result = $"Released {released} expired reservation(s).";
        if (released > 0)
        {
            _logger.LogInformation("Inventory reservation sweep released {Count} expired reservation(s).", released);
        }
    }
}
