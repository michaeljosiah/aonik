using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that polls the AiCallCompleted firehose for the configured
/// look-back window and emits a high-priority warning when estimated AI
/// spend exceeds the threshold. This is the "did we just burn £10 of OpenAI
/// credit again?" alarm that the runaway-spend incident lacked.
///
/// The check is intentionally cheap: it reuses the same KQL the AI tab
/// already issues (<see cref="IObservabilityService.GetAiPerformanceAsync"/>)
/// so we pay one query per tick regardless of caller count.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AiCostGuardJob : IJob
{
    public static readonly JobKey Key = new("AiCostGuardJob", ScheduledJobGroups.ScheduledJobs);

    private readonly IObservabilityService _observabilityService;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<AiCostGuardJob> _logger;

    public AiCostGuardJob(
        IObservabilityService observabilityService,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> options,
        ILogger<AiCostGuardJob> logger)
    {
        _observabilityService = observabilityService;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Seed system tenant context — IObservabilityService doesn't require
        // it, but downstream caches and audit hooks expect a resolved value.
        _tenantContext.TenantId = Guid.Empty;
        _tenantContext.ResolutionSource = "system";

        var settings = _options.AiCostGuard;
        if (!settings.Enabled)
        {
            context.Result = "AI cost guard disabled.";
            return;
        }

        var timeRange = settings.TimeRange;
        var threshold = settings.ThresholdUsd;

        AiPerformanceResponse perf;
        try
        {
            perf = await _observabilityService.GetAiPerformanceAsync(timeRange, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI cost guard could not query observability service.");
            context.Result = "Cost guard query failed.";
            return;
        }

        if (!perf.Configured)
        {
            // No App Insights credentials yet — nothing to do, no alarm.
            context.Result = "Observability not configured.";
            return;
        }

        var totalCost = perf.ByUseCase.Sum(uc => uc.EstimatedCostUsd);
        var totalCalls = perf.ByUseCase.Sum(uc => uc.Calls);

        var topUseCases = perf.ByUseCase
            .OrderByDescending(uc => uc.EstimatedCostUsd)
            .Take(3)
            .Select(uc => $"{uc.UseCase} (${uc.EstimatedCostUsd:F2}, {uc.Calls} calls)")
            .ToList();

        var topModels = perf.ByModel
            .OrderByDescending(m => m.EstimatedCostUsd)
            .Take(3)
            .Select(m => $"{m.Model} (${m.EstimatedCostUsd:F2}, {m.Calls} calls)")
            .ToList();

        if (totalCost >= threshold)
        {
            // LogError so the observability dashboard's Errors tab surfaces
            // it alongside other high-priority signals.
            _logger.LogError(
                "AiCostGuardTripped: TimeRange={TimeRange} EstimatedCostUsd={EstimatedCostUsd:F2} ThresholdUsd={ThresholdUsd:F2} Calls={Calls} TopUseCases={TopUseCases} TopModels={TopModels}",
                timeRange,
                totalCost,
                threshold,
                totalCalls,
                string.Join("; ", topUseCases),
                string.Join("; ", topModels));
        }
        else
        {
            _logger.LogDebug(
                "AI cost guard: ${EstimatedCostUsd:F2} of ${ThresholdUsd:F2} budget over {TimeRange} ({Calls} calls).",
                totalCost,
                threshold,
                timeRange,
                totalCalls);
        }

        context.Result = $"Cost ${totalCost:F2} / threshold ${threshold:F2} over {timeRange} ({totalCalls} calls).";
    }
}
