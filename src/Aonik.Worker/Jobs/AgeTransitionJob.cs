using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Applies the two age transitions and the safety-band moves (Spec 095 §11, Spec 096 §9).
///
/// <para>
/// Thin by design: the flow lives in <see cref="AgeTransitionService"/> so it is testable without a
/// scheduler, and every step of it is idempotent — which is what makes running this on a cron safe
/// rather than merely convenient.
/// </para>
///
/// <para>
/// Runs per tenant because <c>EnforceTenantOnWrites</c> rejects saving a tenant-scoped row whose
/// TenantId is not the ambient one. Every scheduled job in this platform has had to learn that;
/// discovering it at runtime looks like an unrelated persistence failure.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AgeTransitionJob : IJob
{
    public static readonly JobKey Key = new("AgeTransitionJob", ScheduledJobGroups.ScheduledJobs);

    private readonly AgeTransitionService _transitions;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<AgeTransitionJob> _logger;

    public AgeTransitionJob(
        AgeTransitionService transitions,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> options,
        ILogger<AgeTransitionJob> logger)
    {
        _transitions = transitions;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.AgeTransition.Enabled)
        {
            context.Result = "Age transitions disabled.";
            return;
        }

        var notices = 0;
        var consentAge = 0;
        var majority = 0;
        var bands = 0;

        var tenants = await _transitions.FindTenantsWithWorkAsync(context.CancellationToken);

        await TenantScopedJob.ForEachTenantAsync(
            _tenantContext, tenants, "age-transition",
            async ct =>
            {
                var summary = await _transitions.RunAsync(ct);

                notices += summary.NoticesGiven;
                consentAge += summary.ConsentAgeReached;
                majority += summary.MajorityReached;
                bands += summary.SafetyBandsChanged;

                return summary.NoticesGiven + summary.ConsentAgeReached
                    + summary.MajorityReached + summary.SafetyBandsChanged;
            },
            _logger,
            context.CancellationToken);

        context.Result =
            $"Notices {notices}, consent age {consentAge}, majority {majority}, safety bands {bands}.";
    }
}
