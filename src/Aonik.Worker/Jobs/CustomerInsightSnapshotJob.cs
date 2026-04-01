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

    public Task Execute(IJobExecutionContext context)
    {
        return ExecuteAsync(context.JobDetail.JobDataMap, context.CancellationToken);
    }

    internal async Task ExecuteAsync(JobDataMap jobDataMap, CancellationToken cancellationToken)
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
            return;
        }

        _logger.LogInformation(
            "Processing {Count} customer insight snapshot users starting after checkpoint {Checkpoint}.",
            users.Count,
            checkpoint is null ? "<start>" : $"{checkpoint.Value.TenantId}/{checkpoint.Value.UserId}");

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

                LogSnapshotResult(user, snapshot, stopwatch.ElapsedMilliseconds, warningThresholdMs);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(
                    ex,
                    "Customer insight snapshot generation crashed for user {UserId} in tenant {TenantId} after {DurationMs}ms.",
                    user.UserId,
                    user.TenantId,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        _tenantContext.TenantId = null;
        _tenantContext.ResolutionSource = "system";

        if (users.Count < batchSize)
        {
            ClearCheckpoint(jobDataMap);
            return;
        }

        WriteCheckpoint(jobDataMap, users[^1]);
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
}

internal readonly record struct CustomerInsightSnapshotJobCheckpoint(Guid TenantId, Guid UserId);
