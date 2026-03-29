namespace Aonik.SharedKernel.Abstractions.PersonalFinance;

/// <summary>
/// Cross-module contract for the UserBriefProjector to retrieve personal finance
/// data without depending on the Finance module directly.
/// Implemented by FinanceModule, consumed by AgentsModule.
/// </summary>
public interface IUserBriefDataProvider
{
    /// <summary>
    /// Retrieves the financial snapshot data needed for the user brief.
    /// </summary>
    Task<UserBriefFinancialData> GetFinancialDataAsync(
        Guid tenantId,
        Guid userId,
        UserBriefFinancialDataRequest request,
        CancellationToken cancellationToken = default);
}

public record UserBriefFinancialDataRequest(
    int BillLookaheadDays = 14,
    DateTime? SpendPeriodStart = null,
    DateTime? SpendPeriodEnd = null);

public record UserBriefFinancialData(
    // Cash summary
    decimal TotalBalance,
    decimal AvailableBalance,
    string PrimaryCurrency,

    // Upcoming bills
    IReadOnlyList<UserBriefBillData> UpcomingBills,

    // Subscriptions
    IReadOnlyList<UserBriefSubscriptionData> ActiveSubscriptions,

    // Spend summary
    UserBriefSpendData? SpendSummary,

    // Budget pressure
    IReadOnlyList<UserBriefBudgetPressureData> BudgetPressure,

    // Goals
    IReadOnlyList<UserBriefGoalData> ActiveGoals,

    // Support obligations (from FLG party relationships)
    IReadOnlyList<UserBriefObligationData> SupportObligations,

    // Corridor countries from FLG
    IReadOnlyList<string> CorridorCountries,

    // Household context
    string? HouseholdContext);

public record UserBriefBillData(
    Guid BillId,
    string Payee,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    bool Autopay);

public record UserBriefSubscriptionData(
    Guid SubscriptionId,
    string Merchant,
    decimal ExpectedAmount,
    string Currency,
    DateTime RenewalDate);

public record UserBriefSpendData(
    decimal TotalSpend,
    IReadOnlyList<UserBriefCategorySpendData> TopCategories,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record UserBriefCategorySpendData(
    string Category,
    decimal Amount,
    decimal Percentage);

public record UserBriefBudgetPressureData(
    string Category,
    decimal Budgeted,
    decimal Actual,
    decimal PercentUsed);

public record UserBriefGoalData(
    Guid GoalId,
    string Name,
    decimal TargetAmount,
    decimal ProgressAmount,
    string Currency,
    DateTime? TargetDate,
    string Status);

public record UserBriefObligationData(
    string DisplayName,
    decimal? Amount,
    string Currency,
    string? Frequency,
    DateTime? NextDueDate);
