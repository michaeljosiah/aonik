using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphHydrationService
{
    internal const string CoreCacheSet = "personal-finance-graph";
    internal const string FxCacheSet = "personal-finance-graph-fx";
    internal const int TransactionWindowDays = 120;
    internal const int WarningThresholdCount = 1000;

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICacheStore _cacheStore;
    private readonly ILogger<FinancialLifeGraphHydrationService> _logger;

    public FinancialLifeGraphHydrationService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ICacheStore cacheStore,
        ILogger<FinancialLifeGraphHydrationService> logger)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheStore = cacheStore;
        _logger = logger;
    }

    public async Task<FinancialLifeGraphSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var coreCacheKey = FinancialLifeGraphCacheInvalidator.GetCoreCacheKey(tenantId, userId);

        var coreSnapshot = await _cacheStore.GetOrSetAsync(
            coreCacheKey,
            CachePolicy.Medium,
            LoadCoreSnapshotAsync,
            CoreCacheSet,
            cancellationToken)
            ?? await LoadCoreSnapshotAsync(cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph core snapshot could not be created.");

        var relevantAccountCurrencies = GetRelevantAccountCurrencies(coreSnapshot.Accounts, coreSnapshot.LinkedAccounts);
        var fxQuotes = await GetFxQuotesAsync(tenantId, userId, relevantAccountCurrencies, cancellationToken);

        LogSnapshotMetrics(
            tenantId,
            userId,
            coreSnapshot.Transactions.Count,
            coreSnapshot.Bills.Count,
            coreSnapshot.Goals.Count,
            coreSnapshot.Subscriptions.Count,
            coreSnapshot.NativeNodes.Count,
            coreSnapshot.NativeEdges.Count,
            coreSnapshot.Transactions.FirstOrDefault()?.OccurredAt,
            fxQuotes.Count,
            relevantAccountCurrencies.Count);

        return coreSnapshot with { FxQuotes = fxQuotes };
    }

    private async Task<FinancialLifeGraphSnapshot?> LoadCoreSnapshotAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var personalProfile = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.TenantId == tenantId && profile.UserId == userId,
                cancellationToken);

        Household? household = null;
        List<HouseholdMember> householdMembers = new();

        if (personalProfile?.HouseholdId is Guid householdId)
        {
            household = await _financeDbContext.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == householdId, cancellationToken);

            householdMembers = await _financeDbContext.HouseholdMembers
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.HouseholdId == householdId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        var accounts = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(item => item.Id).ToList();

        var linkedAccounts = accountIds.Count == 0
            ? new List<FinancialLinkedAccount>()
            : await _financeDbContext.FinancialLinkedAccounts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.UserId == userId && accountIds.Contains(item.PersonalAccountId))
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

        var transactionCutoff = DateTime.UtcNow.Date.AddDays(-TransactionWindowDays);
        var transactions = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.OccurredAt >= transactionCutoff)
            .OrderByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);

        var bills = await _financeDbContext.Bills
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.NextDueDate)
            .ToListAsync(cancellationToken);

        var goals = await _financeDbContext.Goals
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.TargetDate ?? DateTime.MaxValue)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var subscriptions = await _financeDbContext.Subscriptions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.RenewalDate)
            .ToListAsync(cancellationToken);

        var selfPartyId = personalProfile?.PartyId;
        var relatedParties = new List<PartyReadModel>();
        var partyRelationships = new List<PartyRelationshipReadModel>();

        if (selfPartyId.HasValue)
        {
            partyRelationships = await _financeDbContext.Set<PartyRelationshipReadModel>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.IsActive
                    && (item.FromPartyId == selfPartyId.Value || item.ToPartyId == selfPartyId.Value))
                .OrderBy(item => item.RelationshipTypeCode)
                .ToListAsync(cancellationToken);

            var relatedPartyIds = partyRelationships
                .Select(item => item.FromPartyId == selfPartyId.Value ? item.ToPartyId : item.FromPartyId)
                .Distinct()
                .ToList();

            if (relatedPartyIds.Count > 0)
            {
                relatedParties = await _financeDbContext.Parties
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && relatedPartyIds.Contains(item.Id))
                    .OrderBy(item => item.DisplayName)
                    .ToListAsync(cancellationToken);
            }
        }

        var nativeNodes = await _financeDbContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == FinancialLifeGraphEntityStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var nativeEdges = await _financeDbContext.FinancialLifeGraphEdges
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == FinancialLifeGraphEntityStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return new FinancialLifeGraphSnapshot(
            tenantId,
            userId,
            personalProfile,
            household,
            householdMembers,
            accounts,
            linkedAccounts,
            transactions,
            bills,
            goals,
            subscriptions,
            [],
            selfPartyId,
            relatedParties,
            partyRelationships,
            nativeNodes,
            nativeEdges);
    }

    private async Task<IReadOnlyList<Entities.Pricing.FxQuote>> GetFxQuotesAsync(
        Guid tenantId,
        Guid userId,
        IReadOnlyList<string> relevantAccountCurrencies,
        CancellationToken cancellationToken)
    {
        if (relevantAccountCurrencies.Count < 2)
        {
            return [];
        }

        var fxCacheKey = FinancialLifeGraphCacheInvalidator.GetFxCacheKey(tenantId, userId);

        var quotes = await _cacheStore.GetOrSetAsync(
            fxCacheKey,
            CachePolicy.Short,
            async ct => await _financeDbContext.FxQuotes
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.BaseCurrency != item.TargetCurrency
                    && relevantAccountCurrencies.Contains(item.BaseCurrency)
                    && relevantAccountCurrencies.Contains(item.TargetCurrency))
                .OrderByDescending(item => item.ExpiresAt)
                .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Take(10)
                .ToListAsync(ct),
            FxCacheSet,
            cancellationToken);

        return quotes ?? [];
    }

    private void LogSnapshotMetrics(
        Guid tenantId,
        Guid userId,
        int loadedTransactionCount,
        int billsCount,
        int goalsCount,
        int subscriptionsCount,
        int nativeNodesCount,
        int nativeEdgesCount,
        DateTime? latestTransactionAt,
        int fxQuoteCount,
        int currencyCount)
    {
        _logger.LogInformation(
            "Financial life graph snapshot loaded for tenant {TenantId} user {UserId}. Transactions={LoadedTransactions}, Bills={BillsCount}, Goals={GoalsCount}, Subscriptions={SubscriptionsCount}, NativeNodes={NativeNodesCount}, NativeEdges={NativeEdgesCount}, FxQuotes={FxQuoteCount}, RelevantCurrencies={CurrencyCount}, LatestTransactionAt={LatestTransactionAt}",
            tenantId,
            userId,
            loadedTransactionCount,
            billsCount,
            goalsCount,
            subscriptionsCount,
            nativeNodesCount,
            nativeEdgesCount,
            fxQuoteCount,
            currencyCount,
            latestTransactionAt);

        if (loadedTransactionCount > WarningThresholdCount
            || billsCount > WarningThresholdCount
            || goalsCount > WarningThresholdCount
            || subscriptionsCount > WarningThresholdCount
            || nativeNodesCount > WarningThresholdCount
            || nativeEdgesCount > WarningThresholdCount)
        {
            _logger.LogWarning(
                "Financial life graph snapshot volume is high for tenant {TenantId} user {UserId}. Transactions={LoadedTransactions}, Bills={BillsCount}, Goals={GoalsCount}, Subscriptions={SubscriptionsCount}, NativeNodes={NativeNodesCount}, NativeEdges={NativeEdgesCount}",
                tenantId,
                userId,
                loadedTransactionCount,
                billsCount,
                goalsCount,
                subscriptionsCount,
                nativeNodesCount,
                nativeEdgesCount);
        }
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private static List<string> GetRelevantAccountCurrencies(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<FinancialLinkedAccount> linkedAccounts)
    {
        return accounts
            .Select(item => item.Currency)
            .Concat(linkedAccounts.Select(item => item.Currency))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
