using Aonik.Commerce.Services.Sourcing;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Raises (or refreshes) low-stock alerts for ingredient levels whose available stock is at/below
/// their reorder point (Spec 052 §9). Sweeps across all tenants under a system context; idempotent
/// by construction — at most one ACTIVE (Open/Acknowledged) alert per ingredient, refreshed rather
/// than duplicated, so a double-fired or re-run scan never piles up alerts. Tenants whose Commerce
/// module is off are skipped and counted in the execution result (Spec 097 §12.2).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class LowStockScanJob : IJob
{
    public static readonly JobKey Key = new("LowStockScanJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ILowStockAlertService _alerts;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<LowStockScanJob> _logger;
    private readonly IModuleEnablementReader? _moduleReader;

    public LowStockScanJob(
        ILowStockAlertService alerts,
        IOptions<ScheduledJobOptions> options,
        ILogger<LowStockScanJob> logger,
        IModuleEnablementReader? moduleReader = null)
    {
        _alerts = alerts;
        _options = options.Value;
        _logger = logger;
        _moduleReader = moduleReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.LowStockScan.Enabled)
        {
            context.Result = "Low-stock scan disabled.";
            return;
        }

        var tenants = await _alerts.FindTenantsWithLowStockAsync(context.CancellationToken);
        var gate = await ModuleGatedTenants.FilterAsync(
            _moduleReader, tenants, ModuleIds.Commerce, "Low-stock scan", _logger, context.CancellationToken);

        var result = gate.Enabled.Count == 0
            ? new Aonik.Commerce.Contracts.Models.Sourcing.LowStockScanResult(0, 0)
            : await _alerts.ScanAndRaiseAsync(gate.Enabled, context.CancellationToken);
        context.Result = $"Raised {result.Raised} and refreshed {result.Refreshed} low-stock alert(s)." + gate.Note;
        if (result.Raised > 0)
        {
            _logger.LogInformation(
                "Low-stock scan raised {Raised} new alert(s) and refreshed {Refreshed} active alert(s).",
                result.Raised, result.Refreshed);
        }
    }
}
