using System.Security.Cryptography;
using System.Text;

using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services.Accounts;

internal sealed class AccountTransactionSyncOrchestrator
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;
    private readonly IAccountTransactionCategorizer _categorizer;
    private readonly AccountConnectionSyncOptions _options;
    private readonly ILogger<AccountTransactionSyncOrchestrator> _logger;

    public AccountTransactionSyncOrchestrator(
        PersonalFinanceDbContext financeDbContext,
        ITenantContext tenantContext,
        IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways,
        IAccountTransactionCategorizer categorizer,
        IOptions<AccountConnectionSyncOptions> options,
        ILogger<AccountTransactionSyncOrchestrator> logger)
    {
        _financeDbContext = financeDbContext;
        _tenantContext = tenantContext;
        _providerGateways = providerGateways;
        _categorizer = categorizer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AccountTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid tenantId,
        Guid connectionId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var originalTenantId = _tenantContext.TenantId;
        var originalResolutionSource = _tenantContext.ResolutionSource;
        AccountConnection? connection = null;

        try
        {
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = $"AccountSync:{trigger}";

            var utcNow = DateTime.UtcNow;

            connection = await _financeDbContext.AccountConnections
                .FirstOrDefaultAsync(
                    item => item.Id == connectionId
                        && item.TenantId == tenantId,
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

            var linkedAccounts = await _financeDbContext.Accounts
                .Where(item => item.TenantId == tenantId
                    && item.AccountConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            if (linkedAccounts.Count == 0)
            {
                throw new InvalidOperationException("No linked accounts are available for transaction sync.");
            }

            var linkedAccountsByReference = linkedAccounts
                .Where(item => item.ProviderAccountReference != null)
                .ToDictionary(item => item.ProviderAccountReference!, StringComparer.Ordinal);

            var gateway = ResolveProvider(connection.Provider);
            var syncResult = await gateway.SyncTransactionsAsync(
                new AccountLinkProviderTransactionsSyncRequest(
                    tenantId,
                    Guid.Empty, // UserId not relevant for tenant-scoped
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
                ApplyActionRequiredState(connection, linkedAccounts, syncResult.SyncStatus, syncResult.LastError);

                await _financeDbContext.SaveChangesAsync(cancellationToken);

                return new AccountTransactionSyncResponse(
                    connection.Id, 0, 0, 0, 0,
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

            // Fetch existing transactions for idempotent upsert
            var providerTransactionRefs = syncResult.Transactions
                .Select(item => item.ProviderTransactionReference)
                .Distinct()
                .ToList();

            var existingTransactionsByRef = providerTransactionRefs.Count == 0
                ? new Dictionary<string, AccountTransaction>()
                : await _financeDbContext.AccountTransactions
                    .Where(item => item.TenantId == tenantId
                        && item.AccountConnectionId == connection.Id
                        && providerTransactionRefs.Contains(item.ProviderTransactionReference))
                    .ToDictionaryAsync(item => item.ProviderTransactionReference, cancellationToken);

            // Pre-fetch the tenant's merchant-category rules once. Classification
            // happens per-transaction below but must not hit the DB inside the loop.
            var merchantRules = await _financeDbContext.AccountTransactionMerchantCategories
                .Where(rule => rule.TenantId == tenantId)
                .ToDictionaryAsync(
                    rule => rule.MerchantKey,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

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

                var merchantRule = ResolveMerchantRule(providerTransaction, merchantRules);

                if (!existingTransactionsByRef.TryGetValue(providerTransaction.ProviderTransactionReference, out var transaction))
                {
                    transaction = new AccountTransaction
                    {
                        TenantId = tenantId,
                        AccountId = linkedAccount.Id,
                        AccountConnectionId = connection.Id,
                        ProviderTransactionReference = providerTransaction.ProviderTransactionReference,
                        ReconciliationStatus = "Unmatched"
                    };

                    ApplyProviderTransaction(transaction, providerTransaction);
                    _categorizer.Classify(transaction, providerTransaction, merchantRule);
                    _financeDbContext.AccountTransactions.Add(transaction);
                    existingTransactionsByRef[providerTransaction.ProviderTransactionReference] = transaction;
                    added += 1;
                    continue;
                }

                transaction.AccountId = linkedAccount.Id;
                ApplyProviderTransaction(transaction, providerTransaction);
                _categorizer.Classify(transaction, providerTransaction, merchantRule);
                updated += 1;
            }

            var removed = 0;
            if (syncResult.RemovedTransactionReferences.Count > 0)
            {
                var removedRefs = syncResult.RemovedTransactionReferences.Distinct().ToList();

                var existingRemovedTransactions = await _financeDbContext.AccountTransactions
                    .Where(item => item.TenantId == tenantId
                        && item.AccountConnectionId == connection.Id
                        && removedRefs.Contains(item.ProviderTransactionReference))
                    .ToListAsync(cancellationToken);

                if (existingRemovedTransactions.Count > 0)
                {
                    _financeDbContext.AccountTransactions.RemoveRange(existingRemovedTransactions);
                    removed = existingRemovedTransactions.Count;
                }
            }

            await _financeDbContext.SaveChangesAsync(cancellationToken);

            return new AccountTransactionSyncResponse(
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
                "Account transaction sync failed for connection {ConnectionId} via {Trigger}.",
                connectionId,
                trigger);

            connection.LastSyncStatus = "SyncFailed";
            connection.LastError = LimitText(ex.Message, 1000);
            connection.NextScheduledSyncAt = ComputeFailureRetryAt(connection, DateTime.UtcNow);
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        finally
        {
            _tenantContext.TenantId = originalTenantId;
            _tenantContext.ResolutionSource = originalResolutionSource;
        }
    }

    private static void ApplyProviderTransaction(
        AccountTransaction transaction,
        AccountLinkProviderTransactionResult providerTransaction)
    {
        transaction.OccurredAt = providerTransaction.OccurredAt;
        transaction.Amount = providerTransaction.Amount;
        transaction.Currency = providerTransaction.Currency.Trim().ToUpperInvariant();
        transaction.Counterparty = TrimNullable(providerTransaction.Merchant);
        transaction.Description = TrimNullable(providerTransaction.Description);
        transaction.Pending = providerTransaction.Pending;
        // Category, SubCategory, CategoryMethod, and CategoryConfidence are owned by
        // IAccountTransactionCategorizer (called immediately after this method).
    }

    private static AccountTransactionMerchantCategory? ResolveMerchantRule(
        AccountLinkProviderTransactionResult providerTransaction,
        IReadOnlyDictionary<string, AccountTransactionMerchantCategory> merchantRules)
    {
        if (merchantRules.Count == 0)
        {
            return null;
        }

        var merchantKey = MerchantKeyNormalizer.Normalize(providerTransaction.Merchant)
            ?? MerchantKeyNormalizer.Normalize(providerTransaction.Description);

        return merchantKey is not null && merchantRules.TryGetValue(merchantKey, out var rule)
            ? rule
            : null;
    }

    private static void ApplyActionRequiredState(
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        string syncStatus,
        string message)
    {
        connection.Status = "ActionRequired";
        connection.ConsentStatus = "ActionRequired";
        connection.LastSyncStatus = syncStatus;
        connection.LastError = LimitText(message, 1000);
        connection.NextScheduledSyncAt = null;

        foreach (var linkedAccount in linkedAccounts)
        {
            linkedAccount.Status = "ActionRequired";
            linkedAccount.LastSyncStatus = syncStatus;
            linkedAccount.LastError = LimitText(message, 1000);
        }
    }

    private IPersonalAccountLinkProviderGateway ResolveProvider(string provider)
    {
        var gateway = _providerGateways.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, provider, StringComparison.OrdinalIgnoreCase));

        return gateway ?? throw new ArgumentException($"Unsupported account-link provider '{provider}'.", nameof(provider));
    }

    private DateTime? ComputeNextSyncAt(AccountConnection connection, DateTime syncedAt)
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

    private DateTime? ComputeFailureRetryAt(AccountConnection connection, DateTime utcNow)
    {
        if (!_options.EnableRecurringSync || !connection.AutoSyncEnabled)
        {
            return null;
        }

        return utcNow.AddMinutes(Math.Max(_options.FailureRetryDelayMinutes, 1));
    }

    private static string? TrimNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        return normalized == null ? null
            : normalized.Length <= maxLength ? normalized
            : normalized[..maxLength];
    }
}
