using Aonik.Commerce.Services.Checkout;
using Aonik.Platform.Entities.Operations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Transitions box-cart sessions idle beyond the configured window to Abandoned (Spec 068 A6) so
/// a stale anonymous session cannot pin size-plan authoring forever. Sweeps across all tenants
/// under a system context; clustering-safe via Quartz job storage and the idempotent status
/// transition (only Open, order-less box carts move).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class BoxCartAbandonSweepJob : IJob
{
    public static readonly JobKey Key = new("BoxCartAbandonSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ICartMaintenanceService _maintenance;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<BoxCartAbandonSweepJob> _logger;

    public BoxCartAbandonSweepJob(
        ICartMaintenanceService maintenance,
        IOptions<ScheduledJobOptions> options,
        ILogger<BoxCartAbandonSweepJob> logger)
    {
        _maintenance = maintenance;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.BoxCartAbandonSweep.Enabled)
        {
            context.Result = "Box cart abandon sweep disabled.";
            return;
        }

        var abandoned = await _maintenance.AbandonIdleBoxCartsAsync(cancellationToken: context.CancellationToken);
        context.Result = $"Abandoned {abandoned} idle box cart(s).";
        if (abandoned > 0)
        {
            _logger.LogInformation("Box cart abandon sweep transitioned {Count} idle session(s).", abandoned);
        }
    }
}
