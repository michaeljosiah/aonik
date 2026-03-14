using System.Security.Cryptography;
using System.Text;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialConnectionTransactionSyncOrchestrator
{
    private const string LinkedAccountSyncSourceType = "linked_account_sync";

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;
    private readonly FinancialConnectionSyncOptions _options;
    private readonly ILogger<FinancialConnectionTransactionSyncOrchestrator> _logger;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialConnectionTransactionSyncOrchestrator(
        FinanceDbContext financeDbContext,
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

            var linkedAccounts = await _financeDbContext.FinancialLinkedAccounts
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

            var gateway = ResolveProvider(connection.Provider);
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
                ApplyActionRequiredState(
                    connection,
                    linkedAccounts,
                    personalAccounts,
                    syncResult.SyncStatus,
                    syncResult.LastError);

                await _financeDbContext.SaveChangesAsync(cancellationToken);
                _cacheInvalidator.InvalidateCurrentUserGraph();

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
                personalAccount.Status = "Connected";
                personalAccount.IsArchived = false;
                personalAccount.ClosedAt = null;
            }

            var transactionIds = syncResult.Transactions
                .Select(item => CreateDeterministicGuid(item.ProviderTransactionReference))
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

                var sourceId = CreateDeterministicGuid(providerTransaction.ProviderTransactionReference);
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

                    ApplyProviderTransaction(transaction, providerTransaction);
                    _financeDbContext.PersonalTransactions.Add(transaction);
                    existingTransactionsBySourceId[sourceId] = transaction;
                    added += 1;
                    continue;
                }

                transaction.PersonalAccountId = personalAccount.Id;
                ApplyProviderTransaction(transaction, providerTransaction);
                updated += 1;
            }

            var removed = 0;
            if (syncResult.RemovedTransactionReferences.Count > 0)
            {
                var removedIds = syncResult.RemovedTransactionReferences
                    .Select(CreateDeterministicGuid)
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
            _cacheInvalidator.InvalidateCurrentUserGraph();

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
            connection.LastError = LimitText(ex.Message, 1000);
            connection.NextScheduledSyncAt = ComputeFailureRetryAt(connection, DateTime.UtcNow);
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            _cacheInvalidator.InvalidateCurrentUserGraph();
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

    private static void ApplyActionRequiredState(
        FinancialConnection connection,
        IReadOnlyList<FinancialLinkedAccount> linkedAccounts,
        IReadOnlyList<PersonalAccount> personalAccounts,
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

        foreach (var personalAccount in personalAccounts)
        {
            personalAccount.Status = "ActionRequired";
        }
    }

    private static void ApplyProviderTransaction(
        PersonalTransaction transaction,
        AccountLinkProviderTransactionResult providerTransaction)
    {
        transaction.OccurredAt = providerTransaction.OccurredAt;
        transaction.Amount = providerTransaction.Amount;
        transaction.Currency = providerTransaction.Currency.Trim().ToUpperInvariant();
        transaction.Merchant = TrimNullable(providerTransaction.Merchant);
        transaction.Description = TrimNullable(providerTransaction.Description);

        if (CanApplyProviderCategorisation(transaction))
        {
            transaction.Category = TrimNullable(providerTransaction.Category);
            if (!string.IsNullOrWhiteSpace(transaction.Category))
            {
                transaction.Confidence = 0.55m;
                transaction.CategorisedBy = "provider";
                transaction.ClassificationMethod = "provider";
                transaction.ReviewStatus = "Pending";
                transaction.ReviewedAt = null;
                transaction.ReviewedByUserId = null;
            }
            else
            {
                transaction.Confidence = 0m;
                transaction.CategorisedBy = null;
                transaction.ClassificationMethod = null;
                transaction.ReviewStatus = "Pending";
                transaction.ReviewedAt = null;
                transaction.ReviewedByUserId = null;
            }
        }
    }

    private static bool CanApplyProviderCategorisation(PersonalTransaction transaction)
    {
        return string.IsNullOrWhiteSpace(transaction.ClassificationMethod)
            || string.Equals(transaction.ClassificationMethod, "provider", StringComparison.OrdinalIgnoreCase)
            || string.Equals(transaction.CategorisedBy, "provider", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return new Guid(bytes);
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
