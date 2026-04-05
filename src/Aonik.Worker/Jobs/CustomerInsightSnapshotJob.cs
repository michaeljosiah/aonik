using System.Diagnostics;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
internal sealed class CustomerInsightSnapshotJob : IJob
{
    internal const string CheckpointTenantIdKey = "CustomerInsightSnapshotJob.CheckpointTenantId";
    internal const string CheckpointUserIdKey = "CustomerInsightSnapshotJob.CheckpointUserId";

    public static readonly JobKey Key = new("CustomerInsightSnapshotJob", ScheduledJobGroups.ScheduledJobs);

    private readonly ICustomerInsightSnapshotJobUserEnumerator _userEnumerator;
    private readonly ICustomerInsightSnapshotService _snapshotService;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _jobOptions;
    private readonly ILogger<CustomerInsightSnapshotJob> _logger;

    public CustomerInsightSnapshotJob(
        ICustomerInsightSnapshotJobUserEnumerator userEnumerator,
        ICustomerInsightSnapshotService snapshotService,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> jobOptions,
        ILogger<CustomerInsightSnapshotJob> logger)
    {
        _userEnumerator = userEnumerator;
        _snapshotService = snapshotService;
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

        var options = _jobOptions.CustomerInsightSnapshot;
        var batchSize = Math.Max(options.BatchSize, 1);
        var warningThresholdMs = Math.Max(options.UserWarningThresholdSeconds, 0) * 1000;
        var timeout = TimeSpan.FromSeconds(Math.Max(options.UserTimeoutSeconds, 1));
        var checkpoint = ReadCheckpoint(jobDataMap);

        var users = await _userEnumerator.GetNextBatchAsync(checkpoint, batchSize, cancellationToken);
        if (users.Count == 0)
        {
            ClearCheckpoint(jobDataMap);
            _logger.LogInformation("No eligible customer insight snapshot users found for the current batch.");
            return new ScheduledJobExecutionResult(ScheduledJobRunOutcomes.Succeeded, "No users to process.");
        }

        _logger.LogInformation(
            "Processing {Count} customer insight snapshot users starting after checkpoint {Checkpoint}.",
            users.Count,
            checkpoint is null ? "<start>" : $"{checkpoint.Value.TenantId}/{checkpoint.Value.UserId}");

        var processed = 0;
        var failed = 0;
        var partial = 0;
        var elevatedCashflowStress = 0;
        var transactionsUsed = 0;
        var topSignals = new List<string>();
        var failureDetails = new List<string>();

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _tenantContext.TenantId = user.TenantId;
            _tenantContext.ResolutionSource = "system";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var snapshot = await _snapshotService.GenerateCurrentSnapshotAsync(user.UserId, timeoutCts.Token);
                stopwatch.Stop();

                if (snapshot.Status == CustomerInsightSnapshotContract.StatusFailed)
                {
                    failed++;
                    failureDetails.Add($"{user.UserId:D}: {TrimDetail(snapshot.FailureReason)}");
                }
                else
                {
                    processed++;

                    if (snapshot.Snapshot?.Coverage.IsPartial == true)
                    {
                        partial++;
                    }

                    if (!string.IsNullOrWhiteSpace(snapshot.Snapshot?.Risk.CashflowStressLevel)
                        && !string.Equals(
                            snapshot.Snapshot.Risk.CashflowStressLevel,
                            CustomerInsightSnapshotContract.ConfidenceLow,
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            snapshot.Snapshot.Risk.CashflowStressLevel,
                            CustomerInsightSnapshotContract.SeverityLow,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        elevatedCashflowStress++;
                    }

                    transactionsUsed += snapshot.Snapshot?.Evidence.TransactionCountUsed ?? 0;

                    var topSignalTitle = snapshot.Snapshot?.Signals.FirstOrDefault()?.Title;
                    if (!string.IsNullOrWhiteSpace(topSignalTitle))
                    {
                        topSignals.Add(topSignalTitle.Trim());
                    }
                }

                LogSnapshotResult(user, snapshot, stopwatch.ElapsedMilliseconds, warningThresholdMs);
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
                    "Customer insight snapshot generation crashed for user {UserId} in tenant {TenantId} after {DurationMs}ms.",
                    user.UserId,
                    user.TenantId,
                    stopwatch.ElapsedMilliseconds);

                failureDetails.Add($"{user.UserId:D}: {TrimDetail(ex.Message)}");
            }
        }

        _tenantContext.TenantId = null;
        _tenantContext.ResolutionSource = "system";

        if (users.Count < batchSize)
        {
            ClearCheckpoint(jobDataMap);
        }
        else
        {
            WriteCheckpoint(jobDataMap, users[^1]);
        }

        var executionSummary = BuildExecutionSummary(
            processed,
            failed,
            users.Count,
            partial,
            elevatedCashflowStress,
            transactionsUsed,
            topSignals,
            failureDetails);

        return new ScheduledJobExecutionResult(
            failed > 0 ? ScheduledJobRunOutcomes.Failed : ScheduledJobRunOutcomes.Succeeded,
            executionSummary);
    }

    private void LogSnapshotResult(
        CustomerInsightSnapshotJobUserTarget user,
        CustomerInsightSnapshotResponse snapshot,
        long elapsedMilliseconds,
        int warningThresholdMs)
    {
        var durationMs = snapshot.GenerationDurationMs ?? (int)elapsedMilliseconds;
        var sourceSummary = snapshot.Snapshot?.Evidence.SourceCounts.Count > 0
            ? string.Join(
                ", ",
                snapshot.Snapshot.Evidence.SourceCounts.Select(x => $"{x.Source}:{x.Count}"))
            : "none";

        if (snapshot.Status == CustomerInsightSnapshotContract.StatusFailed)
        {
            _logger.LogWarning(
                "Customer insight snapshot generation failed for user {UserId} in tenant {TenantId} after {DurationMs}ms. Reason: {FailureReason}",
                user.UserId,
                user.TenantId,
                durationMs,
                snapshot.FailureReason);
            return;
        }

        if (durationMs >= warningThresholdMs)
        {
            _logger.LogWarning(
                "Customer insight snapshot generation for user {UserId} in tenant {TenantId} completed in {DurationMs}ms. Partial: {IsPartial}. Source counts: {SourceCounts}",
                user.UserId,
                user.TenantId,
                durationMs,
                snapshot.Snapshot?.Coverage.IsPartial ?? false,
                sourceSummary);
            return;
        }

        _logger.LogInformation(
            "Customer insight snapshot generation for user {UserId} in tenant {TenantId} completed in {DurationMs}ms. Partial: {IsPartial}. Source counts: {SourceCounts}",
            user.UserId,
            user.TenantId,
            durationMs,
            snapshot.Snapshot?.Coverage.IsPartial ?? false,
            sourceSummary);
    }

    internal static CustomerInsightSnapshotJobCheckpoint? ReadCheckpoint(JobDataMap jobDataMap)
    {
        if (!jobDataMap.ContainsKey(CheckpointTenantIdKey)
            || !jobDataMap.ContainsKey(CheckpointUserIdKey)
            || !Guid.TryParse(jobDataMap.GetString(CheckpointTenantIdKey), out var tenantId)
            || !Guid.TryParse(jobDataMap.GetString(CheckpointUserIdKey), out var userId))
        {
            return null;
        }

        return new CustomerInsightSnapshotJobCheckpoint(tenantId, userId);
    }

    internal static void WriteCheckpoint(JobDataMap jobDataMap, CustomerInsightSnapshotJobUserTarget user)
    {
        jobDataMap.Put(CheckpointTenantIdKey, user.TenantId.ToString("D"));
        jobDataMap.Put(CheckpointUserIdKey, user.UserId.ToString("D"));
    }

    internal static void ClearCheckpoint(JobDataMap jobDataMap)
    {
        jobDataMap.Remove(CheckpointTenantIdKey);
        jobDataMap.Remove(CheckpointUserIdKey);
    }

    private static string BuildExecutionSummary(
        int processed,
        int failed,
        int totalUsers,
        int partial,
        int elevatedCashflowStress,
        int transactionsUsed,
        IReadOnlyList<string> topSignals,
        IReadOnlyList<string> failureDetails)
    {
        var segments = new List<string>
        {
            $"Processed {processed} of {totalUsers} users",
            $"failed {failed}",
            $"partial {partial}",
            $"elevated cashflow stress {elevatedCashflowStress}",
            $"transactions used {transactionsUsed}"
        };

        var distinctSignals = topSignals
            .Where(signal => !string.IsNullOrWhiteSpace(signal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (distinctSignals.Count > 0)
        {
            segments.Add($"top signals: {string.Join("; ", distinctSignals)}");
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
            return "Unknown snapshot generation error.";
        }

        var normalized = value.Trim();
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    private static string TrimSummary(string value)
    {
        return value.Length <= 1000 ? value : value[..1000];
    }
}

internal readonly record struct CustomerInsightSnapshotJobCheckpoint(Guid TenantId, Guid UserId);
