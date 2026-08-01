using Aonik.Platform.Entities.Operations;
using Aonik.Subscriptions.Services.Subscriptions;
using Aonik.Subscriptions.Services.Usage;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Bills subscriptions whose period is due, and closes those that asked to be cancelled
/// (Spec 087 §16).
///
/// Thin by design: the flow lives in <see cref="SubscriptionRenewalService"/> so it is testable
/// without a scheduler, and every step of it is idempotent — which is what makes running this on a
/// cron safe rather than merely convenient.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SubscriptionRenewalJob : IJob
{
    public static readonly JobKey Key = new("SubscriptionRenewalJob", ScheduledJobGroups.ScheduledJobs);

    private readonly SubscriptionRenewalService _renewals;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<SubscriptionRenewalJob> _logger;

    public SubscriptionRenewalJob(
        SubscriptionRenewalService renewals,
        IOptions<ScheduledJobOptions> options,
        ILogger<SubscriptionRenewalJob> logger)
    {
        _renewals = renewals;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.SubscriptionRenewal.Enabled)
        {
            context.Result = "Subscription renewal disabled.";
            return;
        }

        var due = await _renewals.FindDueAsync(context.CancellationToken);
        var settled = 0;
        var pastDue = 0;
        var closed = 0;
        var needsReauthorisation = 0;

        foreach (var subscriptionId in due)
        {
            // One failure must not stop the run: these are independent subscriptions, and letting
            // a single bad one block the rest would turn a small problem into an outage.
            try
            {
                switch (await _renewals.RenewAsync(subscriptionId, context.CancellationToken))
                {
                    case RenewalOutcome.Settled: settled++; break;
                    case RenewalOutcome.PastDue: pastDue++; break;
                    case RenewalOutcome.Closed: closed++; break;
                    case RenewalOutcome.NeedsReauthorisation: needsReauthorisation++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Subscription renewal failed for {SubscriptionId}.", subscriptionId);
            }
        }

        context.Result = $"Renewed {settled}, past due {pastDue}, closed {closed}, needs re-authorisation {needsReauthorisation}.";

        if (needsReauthorisation > 0)
        {
            // Distinct from a decline: nothing the job does next will fix these, so they are
            // surfaced rather than left to the retry cadence.
            _logger.LogWarning(
                "{Count} subscription(s) could not renew because their payment mandate is gone and need customer re-authorisation.",
                needsReauthorisation);
        }
    }
}

/// <summary>
/// Retries subscriptions whose payment failed (Spec 087 §12.5).
///
/// Separate from renewal because the cadences differ: renewal runs on the billing boundary, retry
/// runs on a backoff. Mandate-gone failures are excluded at the source — their
/// <c>NextAttemptAt</c> is null, so no amount of retrying picks them up.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SubscriptionDunningJob : IJob
{
    public static readonly JobKey Key = new("SubscriptionDunningJob", ScheduledJobGroups.ScheduledJobs);

    private readonly SubscriptionRenewalService _renewals;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<SubscriptionDunningJob> _logger;

    public SubscriptionDunningJob(
        SubscriptionRenewalService renewals,
        IOptions<ScheduledJobOptions> options,
        ILogger<SubscriptionDunningJob> logger)
    {
        _renewals = renewals;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.SubscriptionDunning.Enabled)
        {
            context.Result = "Subscription dunning disabled.";
            return;
        }

        var retryable = await _renewals.FindRetryableAsync(context.CancellationToken);
        var recovered = 0;
        var expired = 0;

        foreach (var subscriptionId in retryable)
        {
            try
            {
                switch (await _renewals.RetryAsync(subscriptionId, context.CancellationToken))
                {
                    case RenewalOutcome.Settled: recovered++; break;
                    case RenewalOutcome.Expired: expired++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Subscription dunning retry failed for {SubscriptionId}.", subscriptionId);
            }
        }

        context.Result = $"Recovered {recovered}, expired {expired}.";

        if (expired > 0)
            _logger.LogWarning("{Count} subscription(s) exhausted their retries and were expired.", expired);
    }
}

/// <summary>
/// Returns holds left behind by dispatches that never finished (Spec 087 §9).
///
/// Without it a crashed run strands allowance permanently: the units are neither consumed nor
/// available, so a subscriber quietly loses what they paid for and nothing explains why.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class UsageReservationSweepJob : IJob
{
    public static readonly JobKey Key = new("UsageReservationSweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly UsageSweeper _sweeper;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<UsageReservationSweepJob> _logger;

    public UsageReservationSweepJob(
        UsageSweeper sweeper,
        IOptions<ScheduledJobOptions> options,
        ILogger<UsageReservationSweepJob> logger)
    {
        _sweeper = sweeper;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.UsageReservationSweep.Enabled)
        {
            context.Result = "Usage reservation sweep disabled.";
            return;
        }

        var expired = await _sweeper.ExpireStaleReservationsAsync(context.CancellationToken);
        context.Result = $"Expired {expired} stale usage reservation(s).";

        if (expired > 0)
            _logger.LogInformation("Usage reservation sweep returned {Count} stale hold(s).", expired);
    }
}

/// <summary>
/// Closes allowance that has lapsed (Spec 087 §8).
///
/// Changes no subscriber's position — draw-down already ignores an expired grant on read. It exists
/// so breakage is a <b>recorded event</b> rather than something reconstructed from a date
/// comparison months later.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class GrantExpirySweepJob : IJob
{
    public static readonly JobKey Key = new("GrantExpirySweepJob", ScheduledJobGroups.ScheduledJobs);

    private readonly UsageSweeper _sweeper;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<GrantExpirySweepJob> _logger;

    public GrantExpirySweepJob(
        UsageSweeper sweeper,
        IOptions<ScheduledJobOptions> options,
        ILogger<GrantExpirySweepJob> logger)
    {
        _sweeper = sweeper;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.GrantExpirySweep.Enabled)
        {
            context.Result = "Grant expiry sweep disabled.";
            return;
        }

        var closed = await _sweeper.CloseExpiredGrantsAsync(context.CancellationToken);
        context.Result = $"Closed {closed} expired entitlement grant(s).";

        if (closed > 0)
            _logger.LogInformation("Grant expiry sweep closed {Count} lapsed grant(s).", closed);
    }
}
