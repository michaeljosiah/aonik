using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services;

internal sealed class FinancialConnectionTransactionSyncOrchestrator
{
    private const string LinkedAccountSyncSourceType = "linked_account_sync";

    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;
    private readonly FinancialConnectionSyncOptions _options;
    private readonly ILogger<FinancialConnectionTransactionSyncOrchestrator> _logger;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialConnectionTransactionSyncOrchestrator(
        PersonalFinanceDbContext financeDbContext,
        ITenantContext tenantContext,
        IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways,
        IOptions<FinancialConnectionSyncOptions> options,
        ILogger<FinancialConnectionTransactionSyncOrchestrator> logger,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantContext = tenantContext;
        _providerGateways = providerGateways;
        _options = options.Value;
        _logger = logger;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<AccountLinkTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid tenantId,
        Guid userId,
        Guid connectionId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var originalTenantId = _tenantContext.TenantId;
        var originalResolutionSource = _tenantContext.ResolutionSource;
        FinancialConnection? connection = null;

        try
        {
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = $"LinkedAccountSync:{trigger}";

            var utcNow = DateTime.UtcNow;

            connection = await _financeDbContext.FinancialConnections
                .FirstOrDefaultAsync(
                    item => item.Id == connectionId
                        && item.TenantId == tenantId
                        && item.UserId == userId,
                    cancellationToken);

            if (connection == null)
            {
                return null;
            }

            if (connection.DisconnectedAt != null || string.Equals(connection.Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Disconnected account links cannot sync transactions.");
            }

            if (string.Equals(connection.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase)
                || string.Equals(connection.ConsentStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Reconnect this account link before syncing transactions.");
            }

            var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
                .Where(item => item.TenantId == tenantId
                    && item.UserId == userId
                    && item.FinancialConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            if (linkedAccounts.Count == 0)
            {
                throw new InvalidOperationException("No linked accounts are available for transaction sync.");
            }

            var personalAccountIds = linkedAccounts.Select(item => item.PersonalAccountId).Distinct().ToList();
            var personalAccounts = await _financeDbContext.PersonalAccounts
                .Where(item => item.TenantId == tenantId
                    && item.UserId == userId
                    && personalAccountIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

            var personalAccountsById = personalAccounts.ToDictionary(item => item.Id);
            var linkedAccountsByReference = linkedAccounts.ToDictionary(item => item.ProviderAccountReference, StringComparer.Ordinal);
            var linkedAccountsByPersonalAccountId = linkedAccounts
                .GroupBy(item => item.PersonalAccountId)
                .ToDictionary(group => group.Key, group => group.First());

            var gateway = ResolveProvider(connection.Provider);
            var previousDisconnectedAt = connection.DisconnectedAt;
            var syncResult = await gateway.SyncTransactionsAsync(
                new AccountLinkProviderTransactionsSyncRequest(
                    tenantId,
                    userId,
                    connection.Id,
                    connection.ProviderConnectionReference,
                    connection.SecretReference,
                    connection.SyncCursor),
                cancellationToken);

            connection.LastSyncedAt = syncResult.SyncedAt;
            connection.LastSyncStatus = syncResult.SyncStatus;
            connection.SyncCursor = syncResult.NextCursor ?? connection.SyncCursor;

            if (!string.IsNullOrWhiteSpace(syncResult.LastError))
            {
                ProviderTransactionMapper.ApplyActionRequiredState(
                    connection,
                    linkedAccounts,
                    personalAccounts,
                    syncResult.SyncStatus,
                    syncResult.LastError);

                await _financeDbContext.SaveChangesAsync(cancellationToken);
                await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

                return new AccountLinkTransactionSyncResponse(
                    connection.Id,
                    0,
                    0,
                    0,
                    0,
                    syncResult.SyncStatus,
                    connection.SyncCursor,
                    syncResult.SyncedAt);
            }

            connection.Status = "Connected";
            connection.ConsentStatus = "Granted";
            connection.LastError = null;
            connection.DisconnectedAt = null;
            connection.NextScheduledSyncAt = ComputeNextSyncAt(connection, syncResult.SyncedAt);

            foreach (var linkedAccount in linkedAccounts)
            {
                linkedAccount.Status = "Connected";
                linkedAccount.LastSyncedAt = syncResult.SyncedAt;
                linkedAccount.LastSyncStatus = syncResult.SyncStatus;
                linkedAccount.LastError = null;
            }

            foreach (var personalAccount in personalAccounts)
            {
                linkedAccountsByPersonalAccountId.TryGetValue(personalAccount.Id, out var linkedAccount);
                ProviderTransactionMapper.ApplyConnectedPersonalAccountState(personalAccount, linkedAccount, previousDisconnectedAt);
            }

            var transactionIds = syncResult.Transactions
                .Select(item => ProviderTransactionMapper.CreateDeterministicGuid(item.ProviderTransactionReference))
                .Distinct()
                .ToList();

            var existingTransactionsBySourceId = transactionIds.Count == 0
                ? new Dictionary<Guid, PersonalTransaction>()
                : await _financeDbContext.PersonalTransactions
                    .Where(item => item.TenantId == tenantId
                        && item.UserId == userId
                        && item.SourceType == LinkedAccountSyncSourceType
                        && transactionIds.Contains(item.SourceId))
                    .ToDictionaryAsync(item => item.SourceId, cancellationToken);

            var added = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var providerTransaction in syncResult.Transactions)
            {
                if (!linkedAccountsByReference.TryGetValue(providerTransaction.ProviderAccountReference, out var linkedAccount))
                {
                    skipped += 1;
                    continue;
                }

                if (!personalAccountsById.TryGetValue(linkedAccount.PersonalAccountId, out var personalAccount))
                {
                    skipped += 1;
                    continue;
                }

                var sourceId = ProviderTransactionMapper.CreateDeterministicGuid(providerTransaction.ProviderTransactionReference);
                if (!existingTransactionsBySourceId.TryGetValue(sourceId, out var transaction))
                {
                    transaction = new PersonalTransaction
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        PersonalAccountId = personalAccount.Id,
                        SourceType = LinkedAccountSyncSourceType,
                        SourceId = sourceId,
                        TagsJson = "[]"
                    };

                    ProviderTransactionMapper.ApplyProviderTransaction(transaction, providerTransaction);
                    _financeDbContext.PersonalTransactions.Add(transaction);
                    existingTransactionsBySourceId[sourceId] = transaction;
                    added += 1;
                    continue;
                }

                transaction.PersonalAccountId = personalAccount.Id;
                ProviderTransactionMapper.ApplyProviderTransaction(transaction, providerTransaction);
                updated += 1;
            }

            var removed = 0;
            if (syncResult.RemovedTransactionReferences.Count > 0)
            {
                var removedIds = syncResult.RemovedTransactionReferences
                    .Select(ProviderTransactionMapper.CreateDeterministicGuid)
                    .Distinct()
                    .ToList();

                var existingRemovedTransactions = await _financeDbContext.PersonalTransactions
                    .Where(item => item.TenantId == tenantId
                        && item.UserId == userId
                        && item.SourceType == LinkedAccountSyncSourceType
                        && removedIds.Contains(item.SourceId))
                    .ToListAsync(cancellationToken);

                if (existingRemovedTransactions.Count > 0)
                {
                    _financeDbContext.PersonalTransactions.RemoveRange(existingRemovedTransactions);
                    removed = existingRemovedTransactions.Count;
                }
            }

            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

            return new AccountLinkTransactionSyncResponse(
                connection.Id,
                added,
                updated,
                removed,
                skipped,
                syncResult.SyncStatus,
                connection.SyncCursor,
                syncResult.SyncedAt);
        }
        catch (Exception ex) when (connection != null)
        {
            _logger.LogWarning(
                ex,
                "Linked account transaction sync failed for connection {ConnectionId} via {Trigger}.",
                connectionId,
                trigger);

            connection.LastSyncStatus = "SyncFailed";
            connection.LastError = ProviderTransactionMapper.LimitText(ex.Message, 1000);
            connection.NextScheduledSyncAt = ComputeFailureRetryAt(connection, DateTime.UtcNow);
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
            throw;
        }
        finally
        {
            _tenantContext.TenantId = originalTenantId;
            _tenantContext.ResolutionSource = originalResolutionSource;
        }
    }

    private IPersonalAccountLinkProviderGateway ResolveProvider(string provider)
    {
        var gateway = _providerGateways.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, provider, StringComparison.OrdinalIgnoreCase));

        return gateway ?? throw new ArgumentException($"Unsupported account-link provider '{provider}'.", nameof(provider));
    }

    private DateTime? ComputeNextSyncAt(FinancialConnection connection, DateTime syncedAt)
    {
        if (!_options.EnableRecurringSync || !connection.AutoSyncEnabled)
        {
            return null;
        }

        var intervalMinutes = connection.SyncIntervalMinutes > 0
            ? connection.SyncIntervalMinutes
            : _options.DefaultSyncIntervalMinutes;

        return syncedAt.AddMinutes(intervalMinutes);
    }

    private DateTime? ComputeFailureRetryAt(FinancialConnection connection, DateTime utcNow)
    {
        if (!_options.EnableRecurringSync || !connection.AutoSyncEnabled)
        {
            return null;
        }

        return utcNow.AddMinutes(Math.Max(_options.FailureRetryDelayMinutes, 1));
    }
}
