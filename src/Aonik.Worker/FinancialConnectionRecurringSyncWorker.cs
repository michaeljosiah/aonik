using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Worker;

internal sealed class FinancialConnectionRecurringSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly FinancialConnectionSyncOptions _options;
    private readonly ILogger<FinancialConnectionRecurringSyncWorker> _logger;

    public FinancialConnectionRecurringSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<FinancialConnectionSyncOptions> options,
        ILogger<FinancialConnectionRecurringSyncWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableRecurringSync)
        {
            _logger.LogInformation("Linked-account recurring sync worker is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(_options.WorkerPollIntervalSeconds, 10));
        using var timer = new PeriodicTimer(pollInterval);

        _logger.LogInformation(
            "Linked-account recurring sync worker started with poll interval {PollInterval} and batch size {BatchSize}.",
            pollInterval,
            _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueConnectionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring linked-account sync cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task ProcessDueConnectionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<FinancialConnectionTransactionSyncOrchestrator>();

        var utcNow = DateTime.UtcNow;
        var dueConnections = await financeDbContext.FinancialConnections
            .AsNoTracking()
            .Where(connection => connection.AutoSyncEnabled
                && connection.DisconnectedAt == null
                && connection.NextScheduledSyncAt != null
                && connection.NextScheduledSyncAt <= utcNow)
            .OrderBy(connection => connection.NextScheduledSyncAt)
            .Take(Math.Max(_options.BatchSize, 1))
            .Select(connection => new
            {
                connection.Id,
                connection.TenantId,
                connection.UserId
            })
            .ToListAsync(cancellationToken);

        if (dueConnections.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Processing {Count} due linked-account sync jobs.",
            dueConnections.Count);

        foreach (var dueConnection in dueConnections)
        {
            try
            {
                await orchestrator.SyncConnectionTransactionsAsync(
                    dueConnection.TenantId,
                    dueConnection.UserId,
                    dueConnection.Id,
                    "recurring-worker",
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Recurring sync failed for linked account connection {ConnectionId}.",
                    dueConnection.Id);
            }
        }
    }
}
