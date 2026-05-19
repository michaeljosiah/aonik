namespace Aonik.Finance.Contracts.Models.PersonalFinance;

/// <summary>
/// Canonical contract constants for deterministic personal-finance customer snapshots.
/// SourceHash is computed from a canonical envelope containing tenant/user identifiers,
/// window boundaries, rule/version constants, coverage state, and normalized source data.
/// The hash intentionally excludes snapshot row identifiers, audit columns, runtime timing,
/// and any downstream AI interpretation so versioning only changes on material input changes.
/// </summary>
public static class CustomerInsightSnapshotContract
{
    public const string SchemaVersion = "customer_insight_snapshot.v3";
    public const string GeneratorVersion = "customer_insight_snapshot_generator.v3";

    public const string StatusCurrent = "Current";
    public const string StatusSuperseded = "Superseded";
    public const string StatusFailed = "Failed";

    public const string ConfidenceLow = "Low";
    public const string ConfidenceMedium = "Medium";
    public const string ConfidenceHigh = "High";

    public const string SeverityLow = "Low";
    public const string SeverityModerate = "Moderate";
    public const string SeverityHigh = "High";
    public const string SeverityCritical = "Critical";

    public const string MonetaryPolicyNativeCurrency = "native_currency_only";
    public const string TransferPolicyNormalizedTransfers = "normalized_transfer_transactions_only";

    public const int OperationalWindowDays = 30;
    public const int TrendWindowDays = 90;
    public const int BehaviourWindowDays = 180;
    public const int ObligationsLookaheadDays = 30;
    public const decimal BudgetPressureThresholdPercent = 80m;

    public const string SnapshotJsonSchema = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "aonik.finance.customer-insight-snapshot.v1",
  "title": "CustomerInsightSnapshot",
  "type": "object",
  "required": [
    "schemaVersion",
    "userId",
    "tenantId",
    "asOfUtc",
    "analysisWindow",
    "currencyPolicy",
    "currencies",
    "coverage",
    "metrics",
    "signals",
    "risk",
    "evidence"
  ],
  "properties": {
    "schemaVersion": { "type": "string" },
    "userId": { "type": "string", "format": "uuid" },
    "tenantId": { "type": "string", "format": "uuid" },
    "asOfUtc": { "type": "string", "format": "date-time" },
    "analysisWindow": { "type": "object" },
    "currencyPolicy": { "type": "object" },
    "currencies": {
      "type": "array",
      "items": { "type": "string" }
    },
    "coverage": { "type": "object" },
    "metrics": { "type": "object" },
    "signals": {
      "type": "array",
      "items": { "type": "object" }
    },
    "risk": { "type": "object" },
    "evidence": { "type": "object" }
  }
}
""";
}

public record CustomerInsightSnapshotResponse(
    Guid Id,
    Guid UserId,
    string Status,
    DateTime AsOfUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int Version,
    string SourceHash,
    string GeneratedBy,
    int? GenerationDurationMs,
    string? FailureReason,
    Guid? SupersededById,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    CustomerInsightSnapshotDocument? Snapshot);

public record CustomerInsightSnapshotHistoryItemResponse(
    Guid Id,
    string Status,
    DateTime AsOfUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int Version,
    string SourceHash,
    string GeneratedBy,
    int? GenerationDurationMs,
    string? FailureReason,
    Guid? SupersededById,
    bool IsPartial,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record GeneratedCustomerInsightSnapshot(
    DateTime AsOfUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string SourceHash,
    string GeneratedBy,
    string SnapshotJson,
    CustomerInsightSnapshotDocument Snapshot);

public record CustomerInsightSnapshotDocument(
    string SchemaVersion,
    Guid UserId,
    Guid TenantId,
    DateTime AsOfUtc,
    CustomerInsightAnalysisWindow AnalysisWindow,
    CustomerInsightCurrencyPolicy CurrencyPolicy,
    IReadOnlyList<string> Currencies,
    CustomerInsightCoverage Coverage,
    CustomerInsightMetrics Metrics,
    IReadOnlyList<CustomerInsightSignal> Signals,
    CustomerInsightRiskOverview Risk,
    CustomerInsightEvidence Evidence,
    CustomerInsightOrderHistory? OrderHistory,
    CustomerInsightHouseholdContext? HouseholdContext);

public record CustomerInsightAnalysisWindow(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int OperationalWindowDays,
    int TrendWindowDays,
    int BehaviourWindowDays,
    int ObligationsLookaheadDays);

public record CustomerInsightCurrencyPolicy(
    string CanonicalMonetaryView,
    string? ReportingCurrency,
    string? ReportingFxPolicy,
    string TransferTreatment);

public record CustomerInsightCoverage(
    bool IsPartial,
    IReadOnlyList<string> AvailableDomains,
    IReadOnlyList<string> MissingDomains,
    IReadOnlyList<string> OmittedSections,
    IReadOnlyList<string> Warnings);

public record CustomerInsightMetrics(
    CustomerInsightCashPosition CashPosition,
    CustomerInsightIncomeSummary Income,
    CustomerInsightExpenseSummary Expense,
    CustomerInsightCategoryInsights Categories,
    CustomerInsightMerchantInsights Merchants,
    CustomerInsightObligationInsights Obligations,
    CustomerInsightBudgetInsights Budgets,
    CustomerInsightGoalInsights Goals);

public record CustomerInsightCashPosition(
    int AccountCount,
    IReadOnlyList<CustomerInsightMoneyAmount> TotalBalanceByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> AvailableBalanceByCurrency,
    IReadOnlyList<CustomerInsightAccountBalance> BalancesByAccount,
    IReadOnlyList<CustomerInsightConcentrationRatio> LiquidityConcentration);

public record CustomerInsightIncomeSummary(
    int WindowDays,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<CustomerInsightMoneyAmount> TotalInflowsByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> RecurringIncomeEstimateByCurrency,
    string IncomeCadence,
    IReadOnlyList<CustomerInsightSourceAmount> LargestInflowSources,
    IReadOnlyList<CustomerInsightAccountFlow> InflowsByAccount,
    IReadOnlyList<CustomerInsightPeriodDelta> MonthOverMonthDeltaByCurrency);

public record CustomerInsightExpenseSummary(
    int WindowDays,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<CustomerInsightMoneyAmount> TotalOutflowsByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> FixedSpendEstimateByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> VariableSpendEstimateByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> EssentialSpendEstimateByCurrency,
    IReadOnlyList<CustomerInsightMoneyAmount> DiscretionarySpendEstimateByCurrency,
    IReadOnlyList<CustomerInsightAccountFlow> OutflowsByAccount,
    IReadOnlyList<CustomerInsightPeriodDelta> MonthOverMonthDeltaByCurrency,
    IReadOnlyList<CustomerInsightAverageSpend> AverageSpendByCurrency);

public record CustomerInsightCategoryInsights(
    int WindowDays,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<CustomerInsightCategorySpend> TopCategoriesByAmount,
    IReadOnlyList<CustomerInsightCategorySpend> TopCategoriesByShare,
    IReadOnlyList<CustomerInsightCategorySpend> CategoryTrendDeltas,
    IReadOnlyList<CustomerInsightConcentrationRatio> ConcentrationRatios,
    IReadOnlyList<CustomerInsightCategoryMonthlySeries> CategoryMonthlyTrends);

public record CustomerInsightMerchantInsights(
    int WindowDays,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<CustomerInsightMerchantSpend> TopMerchantsByAmount,
    IReadOnlyList<CustomerInsightMerchantFrequency> TopMerchantsByFrequency,
    IReadOnlyList<CustomerInsightRecurringMerchantCandidate> RecurringMerchantCandidates,
    IReadOnlyList<CustomerInsightConcentrationRatio> ConcentrationRatios,
    IReadOnlyList<CustomerInsightMerchantMonthlySeries> TopMerchantMonthlyTrends);

public record CustomerInsightObligationInsights(
    int LookaheadDays,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<CustomerInsightCommitmentItem> UpcomingBills,
    IReadOnlyList<CustomerInsightCommitmentItem> Subscriptions,
    IReadOnlyList<CustomerInsightCommitmentItem> PersonalRecurringBills,
    IReadOnlyList<CustomerInsightCommitmentItem> DebtRepayments,
    IReadOnlyList<CustomerInsightCommitmentItem> SupportObligations,
    IReadOnlyList<CustomerInsightMoneyAmount> TotalUpcomingByCurrency,
    IReadOnlyList<CustomerInsightCoverageRatio> CoverageRatios);

public record CustomerInsightBudgetInsights(
    int ActiveBudgetCount,
    IReadOnlyList<CustomerInsightBudgetSummary> ActiveBudgets,
    IReadOnlyList<CustomerInsightBudgetCategoryUsage> CategoriesAboveThreshold,
    IReadOnlyList<CustomerInsightBudgetCategoryUsage> OverspentCategories,
    IReadOnlyList<CustomerInsightBudgetCategoryUsage> ProjectedPressureCategories);

public record CustomerInsightGoalInsights(
    int ActiveGoalCount,
    IReadOnlyList<CustomerInsightGoalProgress> ActiveGoals,
    string SavingsContributionConsistency);

public record CustomerInsightSignal(
    string SignalKey,
    string Category,
    string Title,
    string Description,
    string Severity,
    string Confidence,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<string> MetricRefs,
    string EvidenceSummary);

public record CustomerInsightRiskOverview(
    string CashflowStressLevel,
    string BudgetPressureLevel,
    IReadOnlyList<string> ConcentrationRisks,
    string MissedObligationRisk,
    IReadOnlyList<string> UnusualActivityIndicators);

public record CustomerInsightEvidence(
    int TransactionCountUsed,
    int ConfirmedTransferCount,
    IReadOnlyList<Guid> AccountIdsUsed,
    DateTime TransactionWindowStartUtc,
    DateTime TransactionWindowEndUtc,
    IReadOnlyList<CustomerInsightSourceCount> SourceCounts,
    IReadOnlyList<CustomerInsightExcludedDataCount> ExcludedDataCounts,
    IReadOnlyList<string> RuleVersions,
    IReadOnlyList<string> Warnings);

public record CustomerInsightMoneyAmount(string Currency, decimal Amount);

public record CustomerInsightAccountBalance(
    Guid AccountId,
    string AccountName,
    string AccountType,
    string Currency,
    decimal CurrentBalance,
    decimal BalanceShare);

public record CustomerInsightAccountFlow(
    Guid? AccountId,
    string AccountName,
    string Currency,
    decimal Amount,
    int TransactionCount);

public record CustomerInsightConcentrationRatio(string Currency, decimal Ratio);

public record CustomerInsightSourceAmount(
    string Source,
    string Currency,
    decimal Amount,
    int TransactionCount);

public record CustomerInsightPeriodDelta(
    string Currency,
    decimal CurrentAmount,
    decimal PreviousAmount,
    decimal DeltaAmount,
    decimal? DeltaPercentage);

public record CustomerInsightAverageSpend(
    string Currency,
    decimal WeeklyAverage,
    decimal MonthlyAverage);

public record CustomerInsightCategorySpend(
    string Category,
    string Currency,
    decimal Amount,
    decimal ShareOfSpend,
    int TransactionCount,
    decimal PreviousPeriodAmount,
    decimal? DeltaPercentage);

public record CustomerInsightMerchantSpend(
    string Merchant,
    string Currency,
    decimal Amount,
    decimal ShareOfSpend,
    int TransactionCount);

public record CustomerInsightMerchantFrequency(
    string Merchant,
    string Currency,
    int TransactionCount,
    decimal Amount);

public record CustomerInsightRecurringMerchantCandidate(
    string Merchant,
    string Currency,
    decimal AverageAmount,
    int ObservedMonths,
    int TransactionCount);

public record CustomerInsightCommitmentItem(
    string SourceType,
    Guid SourceId,
    string DisplayName,
    string Currency,
    decimal Amount,
    DateTime DueDate,
    string? Frequency);

public record CustomerInsightCoverageRatio(
    string Currency,
    decimal AvailableBalance,
    decimal UpcomingObligations,
    decimal? Ratio);

public record CustomerInsightBudgetSummary(
    Guid BudgetId,
    DateTime PeriodStart,
    string PeriodType,
    int LineCount,
    string Status);

public record CustomerInsightBudgetCategoryUsage(
    Guid BudgetId,
    Guid BudgetLineId,
    string Category,
    string Currency,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal PercentUsed,
    decimal ProjectedMonthEndAmount,
    bool IsProjectedToOverspend);

public record CustomerInsightGoalProgress(
    Guid GoalId,
    string Name,
    string Currency,
    decimal TargetAmount,
    decimal ProgressAmount,
    decimal ProgressPercent,
    DateTime? TargetDate,
    decimal? EstimatedMonthlyContribution,
    int? EstimatedMonthsToTarget);

public record CustomerInsightSourceCount(string Source, int Count);

public record CustomerInsightExcludedDataCount(string Source, int Count, string Reason);

public record CustomerInsightMonthlySeries(
    IReadOnlyList<string> MonthLabels,
    IReadOnlyList<decimal> Amounts);

public record CustomerInsightCategoryMonthlySeries(
    string Category,
    string Currency,
    CustomerInsightMonthlySeries Series);

public record CustomerInsightMerchantMonthlySeries(
    string Merchant,
    string Currency,
    CustomerInsightMonthlySeries Series);

public record CustomerInsightOrderHistory(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int TotalOrders,
    int CompletedCount,
    int PendingCount,
    int FailedCount,
    IReadOnlyList<CustomerInsightRecentOrder> RecentOrders,
    IReadOnlyList<CustomerInsightOrderTypeSummary> ByType);

public record CustomerInsightRecentOrder(
    Guid OrderId,
    string OrderType,
    string Status,
    string CurrencyIn,
    decimal AmountIn,
    string? CurrencyOut,
    decimal? AmountOut,
    DateTime CreatedAt);

public record CustomerInsightOrderTypeSummary(
    string OrderType,
    int TotalCount,
    int CompletedCount,
    int FailedCount);

public record CustomerInsightHouseholdContext(
    Guid HouseholdId,
    string HouseholdName,
    int MemberCount,
    IReadOnlyList<CustomerInsightHouseholdMemberSummary> Members);

public record CustomerInsightHouseholdMemberSummary(
    Guid UserId,
    string Role,
    bool IsCurrentUser);
