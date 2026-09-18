using Aonik.Commerce.Services.Checkout;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Modules;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Transitions box-cart sessions idle beyond the configured window to Abandoned (Spec 068 A6) so
/// a stale anonymous session cannot pin size-plan authoring forever. Sweeps across all tenants
/// under a system context; clustering-safe via Quartz job storage and the idempotent status
/// transition (only Open, order-less box carts move). Tenants whose Commerce module is off are
/// skipped and counted in the execution result (Spec 097 §12.2).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class BoxCartAbandonSweepJob : IJob
{
    public static readonly JobKey Key = new("BoxCartAbandonSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ICartMaintenanceService _maintenance;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<BoxCartAbandonSweepJob> _logger;
    private readonly IModuleEnablementReader? _moduleReader;

    public BoxCartAbandonSweepJob(
        ICartMaintenanceService maintenance,
        IOptions<ScheduledJobOptions> options,
        ILogger<BoxCartAbandonSweepJob> logger,
        IModuleEnablementReader? moduleReader = null)
    {
        _maintenance = maintenance;
        _options = options.Value;
        _logger = logger;
        _moduleReader = moduleReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.BoxCartAbandonSweep.Enabled)
        {
            context.Result = "Box cart abandon sweep disabled.";
            return;
        }

        var tenants = await _maintenance.FindTenantsWithIdleBoxCartsAsync(cancellationToken: context.CancellationToken);
        var gate = await ModuleGatedTenants.FilterAsync(
            _moduleReader, tenants, ModuleIds.Commerce, "Box cart abandon sweep", _logger, context.CancellationToken);

        var abandoned = gate.Enabled.Count == 0
            ? 0
            : await _maintenance.AbandonIdleBoxCartsAsync(tenantIds: gate.Enabled, cancellationToken: context.CancellationToken);
        context.Result = $"Abandoned {abandoned} idle box cart(s)." + gate.Note;
        if (abandoned > 0)
        {
            _logger.LogInformation("Box cart abandon sweep transitioned {Count} idle session(s).", abandoned);
        }
    }
}
