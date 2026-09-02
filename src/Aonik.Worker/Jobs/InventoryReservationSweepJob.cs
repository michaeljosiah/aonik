using Aonik.Commerce.Services.Inventory;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Releases expired held inventory reservations (Spec 042 §10) so abandoned checkouts free stock.
/// Sweeps across all tenants under a system context; clustering-safe via Quartz job storage and the
/// idempotent status transition (only <c>Held</c> rows are released). Tenants whose Commerce module
/// is off are skipped and counted in the execution result (Spec 097 §12.2).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class InventoryReservationSweepJob : IJob
{
    public static readonly JobKey Key = new("InventoryReservationSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IInventoryService _inventory;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<InventoryReservationSweepJob> _logger;
    private readonly IModuleEnablementReader? _moduleReader;

    public InventoryReservationSweepJob(
        IInventoryService inventory,
        IOptions<ScheduledJobOptions> options,
        ILogger<InventoryReservationSweepJob> logger,
        IModuleEnablementReader? moduleReader = null)
    {
        _inventory = inventory;
        _options = options.Value;
        _logger = logger;
        _moduleReader = moduleReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.InventoryReservationSweep.Enabled)
        {
            context.Result = "Inventory reservation sweep disabled.";
            return;
        }

        var tenants = await _inventory.FindTenantsWithExpiredReservationsAsync(cancellationToken: context.CancellationToken);
        var gate = await ModuleGatedTenants.FilterAsync(
            _moduleReader, tenants, ModuleIds.Commerce, "Inventory reservation sweep", _logger, context.CancellationToken);

        var released = gate.Enabled.Count == 0
            ? 0
            : await _inventory.ReleaseExpiredAsync(tenantIds: gate.Enabled, cancellationToken: context.CancellationToken);
        context.Result = $"Released {released} expired reservation(s)." + gate.Note;
        if (released > 0)
        {
            _logger.LogInformation("Inventory reservation sweep released {Count} expired reservation(s).", released);
        }
    }
}
