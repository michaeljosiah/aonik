using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphSnapshotMetrics
{
    private readonly ILogger<FinancialLifeGraphSnapshotMetrics> _logger;

    public FinancialLifeGraphSnapshotMetrics(ILogger<FinancialLifeGraphSnapshotMetrics> logger)
    {
        _logger = logger;
    }

    public void LogSnapshotLoaded(
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
        int currencyCount,
        int fundingRelationshipCount,
        int inferredAnnotationCount)
    {
        _logger.LogInformation(
            "Financial life graph snapshot loaded for tenant {TenantId} user {UserId}. Transactions={LoadedTransactions}, Bills={BillsCount}, Goals={GoalsCount}, Subscriptions={SubscriptionsCount}, NativeNodes={NativeNodesCount}, NativeEdges={NativeEdgesCount}, FxQuotes={FxQuoteCount}, RelevantCurrencies={CurrencyCount}, FundingRelationships={FundingRelationshipCount}, InferredAnnotations={InferredAnnotationCount}, LatestTransactionAt={LatestTransactionAt}",
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
            fundingRelationshipCount,
            inferredAnnotationCount,
            latestTransactionAt);

        if (loadedTransactionCount > FinancialLifeGraphHydrationService.WarningThresholdCount
            || billsCount > FinancialLifeGraphHydrationService.WarningThresholdCount
            || goalsCount > FinancialLifeGraphHydrationService.WarningThresholdCount
            || subscriptionsCount > FinancialLifeGraphHydrationService.WarningThresholdCount
            || nativeNodesCount > FinancialLifeGraphHydrationService.WarningThresholdCount
            || nativeEdgesCount > FinancialLifeGraphHydrationService.WarningThresholdCount)
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
}
