using System.Diagnostics;

using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
internal sealed class CustomerInsightAiSummaryJob : IJob
{
    internal const string CheckpointTenantIdKey = "CustomerInsightAiSummaryJob.CheckpointTenantId";
    internal const string CheckpointUserIdKey = "CustomerInsightAiSummaryJob.CheckpointUserId";
    internal const string CheckpointSnapshotIdKey = "CustomerInsightAiSummaryJob.CheckpointSnapshotId";

    public static readonly JobKey Key = new("CustomerInsightAiSummaryJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ICustomerInsightAiSummaryJobSnapshotEnumerator _snapshotEnumerator;
    private readonly ICustomerInsightAiSummaryService _summaryService;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _jobOptions;
    private readonly ILogger<CustomerInsightAiSummaryJob> _logger;

    public CustomerInsightAiSummaryJob(
        ICustomerInsightAiSummaryJobSnapshotEnumerator snapshotEnumerator,
        ICustomerInsightAiSummaryService summaryService,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> jobOptions,
        ILogger<CustomerInsightAiSummaryJob> logger)
    {
        _snapshotEnumerator = snapshotEnumerator;
        _summaryService = summaryService;
        _tenantContext = tenantContext;
        _jobOptions = jobOptions.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var execution = await ExecuteAsync(context.JobDetail.JobDataMap, context.CancellationToken);
        context.Result = execution;
    }

    internal async Task<ScheduledJobExecutionResult> ExecuteAsync(JobDataMap jobDataMap, CancellationToken cancellationToken)
    {
        _tenantContext.TenantId = null;
        _tenantContext.ResolutionSource = "system";

        var options = _jobOptions.CustomerInsightAiSummary;
        var batchSize = Math.Max(options.BatchSize, 1);
        var warningThresholdMs = Math.Max(options.SnapshotWarningThresholdSeconds, 0) * 1000;
        var timeout = TimeSpan.FromSeconds(Math.Max(options.SnapshotTimeoutSeconds, 1));
        var checkpoint = ReadCheckpoint(jobDataMap);

        var snapshots = await _snapshotEnumerator.GetNextBatchAsync(checkpoint, batchSize, cancellationToken);
        if (snapshots.Count == 0)
        {
            ClearCheckpoint(jobDataMap);
            _logger.LogInformation("No customer insight snapshots were due for AI summary processing in this batch.");
            return new ScheduledJobExecutionResult(ScheduledJobRunOutcomes.Succeeded, "No snapshots to process.");
        }

        var processed = 0;
        var failed = 0;
        var headlines = new List<string>();
        var failureDetails = new List<string>();

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _tenantContext.TenantId = snapshot.TenantId;
            _tenantContext.ResolutionSource = "system";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var summary = await _summaryService.GenerateCurrentSummaryAsync(snapshot.CustomerInsightSnapshotId, timeoutCts.Token);
                stopwatch.Stop();

                if (summary.Status == CustomerInsightAiSummaryContract.StatusFailed)
                {
                    failed++;
                    failureDetails.Add($"{snapshot.CustomerInsightSnapshotId:D}: {TrimDetail(summary.FailureReason)}");
                }
                else
                {
                    processed++;

                    if (!string.IsNullOrWhiteSpace(summary.Summary?.Headline))
                    {
                        headlines.Add(summary.Summary.Headline.Trim());
                    }
                }

                LogSummaryResult(snapshot, summary, stopwatch.ElapsedMilliseconds, warningThresholdMs);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                failed++;
                _logger.LogWarning(
                    ex,
                    "Customer insight AI summary generation crashed for snapshot {SnapshotId} after {DurationMs}ms.",
                    snapshot.CustomerInsightSnapshotId,
                    stopwatch.ElapsedMilliseconds);

                failureDetails.Add($"{snapshot.CustomerInsightSnapshotId:D}: {TrimDetail(ex.Message)}");
            }
        }

        _tenantContext.TenantId = null;
        _tenantContext.ResolutionSource = "system";

        if (snapshots.Count < batchSize)
        {
            ClearCheckpoint(jobDataMap);
        }
        else
        {
            WriteCheckpoint(jobDataMap, snapshots[^1]);
        }

        var executionSummary = BuildExecutionSummary(processed, failed, snapshots.Count, headlines, failureDetails);

        return new ScheduledJobExecutionResult(
            failed > 0 ? ScheduledJobRunOutcomes.Failed : ScheduledJobRunOutcomes.Succeeded,
            executionSummary);
    }

    private void LogSummaryResult(
        CustomerInsightAiSummaryJobSnapshotTarget snapshot,
        CustomerInsightAiSummaryResponse summary,
        long elapsedMilliseconds,
        int warningThresholdMs)
    {
        if (summary.Status == CustomerInsightAiSummaryContract.StatusFailed)
        {
            _logger.LogWarning(
                "Customer insight AI summary generation failed for snapshot {SnapshotId} after {DurationMs}ms. Reason: {FailureReason}",
                snapshot.CustomerInsightSnapshotId,
                elapsedMilliseconds,
                summary.FailureReason);
            return;
        }

        if (elapsedMilliseconds >= warningThresholdMs)
        {
            _logger.LogWarning(
                "Customer insight AI summary generation for snapshot {SnapshotId} completed in {DurationMs}ms with narrative version {NarrativeVersion}.",
                snapshot.CustomerInsightSnapshotId,
                elapsedMilliseconds,
                summary.NarrativeVersion);
            return;
        }

        _logger.LogInformation(
            "Customer insight AI summary generation for snapshot {SnapshotId} completed in {DurationMs}ms with narrative version {NarrativeVersion}.",
            snapshot.CustomerInsightSnapshotId,
            elapsedMilliseconds,
            summary.NarrativeVersion);
    }

    internal static CustomerInsightAiSummaryJobCheckpoint? ReadCheckpoint(JobDataMap jobDataMap)
    {
        if (!jobDataMap.ContainsKey(CheckpointTenantIdKey)
            || !jobDataMap.ContainsKey(CheckpointUserIdKey)
            || !jobDataMap.ContainsKey(CheckpointSnapshotIdKey)
            || !Guid.TryParse(jobDataMap.GetString(CheckpointTenantIdKey), out var tenantId)
            || !Guid.TryParse(jobDataMap.GetString(CheckpointUserIdKey), out var userId)
            || !Guid.TryParse(jobDataMap.GetString(CheckpointSnapshotIdKey), out var snapshotId))
        {
            return null;
        }

        return new CustomerInsightAiSummaryJobCheckpoint(tenantId, userId, snapshotId);
    }

    internal static void WriteCheckpoint(JobDataMap jobDataMap, CustomerInsightAiSummaryJobSnapshotTarget snapshot)
    {
        jobDataMap.Put(CheckpointTenantIdKey, snapshot.TenantId.ToString("D"));
        jobDataMap.Put(CheckpointUserIdKey, snapshot.UserId.ToString("D"));
        jobDataMap.Put(CheckpointSnapshotIdKey, snapshot.CustomerInsightSnapshotId.ToString("D"));
    }

    internal static void ClearCheckpoint(JobDataMap jobDataMap)
    {
        jobDataMap.Remove(CheckpointTenantIdKey);
        jobDataMap.Remove(CheckpointUserIdKey);
        jobDataMap.Remove(CheckpointSnapshotIdKey);
    }

    private static string BuildExecutionSummary(
        int processed,
        int failed,
        int totalSnapshots,
        IReadOnlyList<string> headlines,
        IReadOnlyList<string> failureDetails)
    {
        var segments = new List<string>
        {
            $"Processed {processed} of {totalSnapshots} snapshots",
            $"failed {failed}"
        };

        var distinctHeadlines = headlines
            .Where(headline => !string.IsNullOrWhiteSpace(headline))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (distinctHeadlines.Count > 0)
        {
            segments.Add($"headlines: {string.Join(" | ", distinctHeadlines)}");
        }

        if (failureDetails.Count > 0)
        {
            segments.Add($"failures: {string.Join(" | ", failureDetails.Take(3))}");
        }

        return TrimSummary(string.Join(". ", segments) + ".");
    }

    private static string TrimDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown AI summary generation error.";
        }

        var normalized = value.Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private static string TrimSummary(string value)
    {
        return value.Length <= 1000 ? value : value[..1000];
    }
}

internal readonly record struct CustomerInsightAiSummaryJobCheckpoint(Guid TenantId, Guid UserId, Guid CustomerInsightSnapshotId);
