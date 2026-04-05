using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.Platform.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that synchronises linked financial account transactions
/// for connections that are due for a recurring sync.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class FinancialConnectionRecurringSyncJob : IJob
{
    public static readonly JobKey Key = new("FinancialConnectionRecurringSyncJob", ScheduledJobGroups.ScheduledJobs);

    private readonly FinanceDbContext _financeDbContext;
    private readonly FinancialConnectionTransactionSyncOrchestrator _orchestrator;
    private readonly ScheduledJobOptions _jobOptions;
    private readonly FinancialConnectionSyncOptions _syncOptions;
    private readonly ILogger<FinancialConnectionRecurringSyncJob> _logger;

    public FinancialConnectionRecurringSyncJob(
        FinanceDbContext financeDbContext,
        FinancialConnectionTransactionSyncOrchestrator orchestrator,
        IOptions<ScheduledJobOptions> jobOptions,
        IOptions<FinancialConnectionSyncOptions> syncOptions,
        ILogger<FinancialConnectionRecurringSyncJob> logger)
    {
        _financeDbContext = financeDbContext;
        _orchestrator = orchestrator;
        _jobOptions = jobOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_syncOptions.EnableRecurringSync)
        {
            _logger.LogDebug("Linked-account recurring sync is disabled via configuration.");
            context.Result = "Recurring sync disabled.";
            return;
        }

        var cancellationToken = context.CancellationToken;
        var batchSize = Math.Max(_jobOptions.FinancialConnectionSync.BatchSize, 1);
        var utcNow = DateTime.UtcNow;

        var dueConnections = await _financeDbContext.FinancialConnections
            .AsNoTracking()
            .Where(c => c.AutoSyncEnabled
                && c.DisconnectedAt == null
                && c.NextScheduledSyncAt != null
                && c.NextScheduledSyncAt <= utcNow)
            .OrderBy(c => c.NextScheduledSyncAt)
            .Take(batchSize)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.UserId
            })
            .ToListAsync(cancellationToken);

        if (dueConnections.Count == 0)
        {
            context.Result = "No connections due for sync.";
            return;
        }

        _logger.LogInformation(
            "Processing {Count} due linked-account sync jobs.",
            dueConnections.Count);

        var synced = 0;
        var failed = 0;

        foreach (var connection in dueConnections)
        {
            try
            {
                await _orchestrator.SyncConnectionTransactionsAsync(
                    connection.TenantId,
                    connection.UserId,
                    connection.Id,
                    "recurring-worker",
                    cancellationToken);
                synced++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "Recurring sync failed for linked account connection {ConnectionId}.",
                    connection.Id);
            }
        }

        context.Result = $"Synced {synced}, failed {failed} of {dueConnections.Count} connections.";
    }
}
