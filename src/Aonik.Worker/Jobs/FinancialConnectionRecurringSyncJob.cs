using Aonik.PersonalFinance.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
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

    // Spec 027 S3 (#126): FinancialConnection is owned by PersonalFinanceDbContext.
    private readonly PersonalFinanceDbContext _personalFinanceDbContext;
    private readonly FinancialConnectionTransactionSyncOrchestrator _orchestrator;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _jobOptions;
    private readonly FinancialConnectionSyncOptions _syncOptions;
    private readonly ILogger<FinancialConnectionRecurringSyncJob> _logger;
    private readonly IModuleEnablementReader? _moduleReader;

    public FinancialConnectionRecurringSyncJob(
        PersonalFinanceDbContext personalFinanceDbContext,
        FinancialConnectionTransactionSyncOrchestrator orchestrator,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> jobOptions,
        IOptions<FinancialConnectionSyncOptions> syncOptions,
        ILogger<FinancialConnectionRecurringSyncJob> logger,
        IModuleEnablementReader? moduleReader = null)
    {
        _personalFinanceDbContext = personalFinanceDbContext;
        _orchestrator = orchestrator;
        _tenantContext = tenantContext;
        _jobOptions = jobOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
        _moduleReader = moduleReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Seed a system-tenant context up-front so any service we call (or any
        // EF query filter / cache invalidator on the path) finds a resolved
        // context instead of throwing "Tenant context not available".
        // A null TenantId is the system "see-all" sentinel: the tenant query
        // filter fails open only for null, never for Guid.Empty (which would
        // scope every query to a non-existent tenant and return zero rows).
        // The orchestrator overrides TenantId per-connection inside its loop.
        _tenantContext.TenantId = null;
        _tenantContext.ResolutionSource = "system";

        if (!_syncOptions.EnableRecurringSync)
        {
            _logger.LogDebug("Linked-account recurring sync is disabled via configuration.");
            context.Result = "Recurring sync disabled.";
            return;
        }

        var cancellationToken = context.CancellationToken;
        var batchSize = Math.Max(_jobOptions.FinancialConnectionSync.BatchSize, 1);
        var utcNow = DateTime.UtcNow;

        // Spec 097 §12.2: the module gate runs BEFORE the batch is taken. Connections of tenants
        // with Personal Finance off are never synced and their NextScheduledSyncAt never advances,
        // so if they were selected first they would keep the oldest due times, sort to the head of
        // every batch and starve enabled tenants once the disabled backlog reached the batch size.
        // Narrowing to enabled tenants first means a disabled tenant never occupies a batch slot,
        // and nothing is written for a module that is off.
        var dueTenants = await _personalFinanceDbContext.FinancialConnections
            .AcrossTenants()
            .AsNoTracking()
            .Where(c => c.AutoSyncEnabled
                && c.DisconnectedAt == null
                && c.NextScheduledSyncAt != null
                && c.NextScheduledSyncAt <= utcNow)
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (dueTenants.Count == 0)
        {
            context.Result = "No connections due for sync.";
            return;
        }

        var gate = await ModuleGatedTenants.FilterAsync(
            _moduleReader,
            dueTenants,
            ModuleIds.PersonalFinance,
            "Financial connection recurring sync",
            _logger,
            cancellationToken);
        var enabledTenants = gate.Enabled.ToList();

        if (enabledTenants.Count == 0)
        {
            context.Result = "No connections due for sync in tenants with Personal Finance enabled." + gate.Note;
            return;
        }

        var dueConnections = await _personalFinanceDbContext.FinancialConnections
            .AcrossTenants()
            .AsNoTracking()
            .Where(c => c.AutoSyncEnabled
                && c.DisconnectedAt == null
                && c.NextScheduledSyncAt != null
                && c.NextScheduledSyncAt <= utcNow
                && enabledTenants.Contains(c.TenantId))
            .OrderBy(c => c.NextScheduledSyncAt)
            .Take(batchSize)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.UserId
            })
            .ToListAsync(cancellationToken);

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

        context.Result = $"Synced {synced}, failed {failed} of {dueConnections.Count} connections." + gate.Note;
    }
}
