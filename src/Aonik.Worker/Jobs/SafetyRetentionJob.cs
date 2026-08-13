using Aonik.Ai.Services.Safety;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Enforces the Spec 096 §13 retention rules — deletes expired blocked content, anonymises expired
/// decisions, and skips anything under a §12 legal hold.
///
/// <para>
/// Ships with the gate rather than after it. An expiry column deletes nothing, and blocked content
/// about children would otherwise be retained indefinitely — invisible precisely because the column
/// looks like the mechanism.
/// </para>
///
/// <para>
/// A failure to run is a <strong>retention incident</strong>, not a quiet backlog: the job records
/// its outcome so a monitored absence is visible, because "the sweeper stopped six weeks ago" is
/// exactly the sort of thing nobody notices until it is asked about in a subject-access request.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SafetyRetentionJob : IJob
{
    public static readonly JobKey Key = new("SafetyRetentionJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ISafetyRetentionSweeper _sweeper;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<SafetyRetentionJob> _logger;

    public SafetyRetentionJob(
        ISafetyRetentionSweeper sweeper,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> options,
        ILogger<SafetyRetentionJob> logger)
    {
        _sweeper = sweeper;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.SafetyRetention.Enabled)
        {
            // Deliberately logged at warning: disabling retention on a table of children's blocked
            // content is not a routine configuration choice and should be visible in the logs.
            _logger.LogWarning("Safety retention sweep is DISABLED; expired child content is being retained.");
            context.Result = "Safety retention disabled.";
            return;
        }

        var deleted = 0;
        var held = 0;
        var anonymised = 0;
        var incidents = 0;

        var tenants = await _sweeper.FindTenantsWithWorkAsync(context.CancellationToken);

        await TenantScopedJob.ForEachTenantAsync(
            _tenantContext, tenants, "safety-retention",
            async ct =>
            {
                var summary = await _sweeper.SweepAsync(ct);

                deleted += summary.ArtefactsDeleted;
                held += summary.ArtefactsHeld;
                anonymised += summary.DecisionsAnonymised;
                incidents += summary.IncidentsDeleted;

                return summary.ArtefactsDeleted + summary.DecisionsAnonymised + summary.IncidentsDeleted;
            },
            _logger,
            context.CancellationToken);

        context.Result =
            $"Artefacts deleted {deleted} (held {held}), decisions anonymised {anonymised}, incidents deleted {incidents}.";
    }
}
