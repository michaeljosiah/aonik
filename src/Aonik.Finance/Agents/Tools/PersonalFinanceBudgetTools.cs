using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance budget tools plus the spending-breakdown read tools
/// (category / merchant / account / summary / merchant-history). Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceBudgetTools
{
    private readonly IBudgetService _budgetService;
    private readonly IPersonalFinanceInsightsService _insightsService;

    public PersonalFinanceBudgetTools(
        IBudgetService budgetService,
        IPersonalFinanceInsightsService insightsService)
    {
        _budgetService = budgetService;
        _insightsService = insightsService;
    }

    // ── Budget Read Tool ──────────────────────────────────────────

    [Description("Lists the user's budget categories for the current month. Each category returns its line-item ID, display name, allocated amount, and spent-to-date amount, plus a short spending history. Use this to answer 'what's in my budget', 'how much have I spent vs allocated', 'am I over budget on X', and similar questions.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> ListBudgets(
        CancellationToken cancellationToken = default)
    {
        return await _budgetService.ListBudgetsAsync(cancellationToken);
    }

    // ── Insights Read Tools ───────────────────────────────────────

    [Description("Gets a spending summary for a given period. Returns total income, total expenses, net savings, and transaction count. Optionally scoped to a specific account.")]
    public async Task<SpendingSummaryResponse> GetSpendingSummary(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetSpendingSummaryAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
    }

    [Description("Gets spending broken down by category for a given period. Returns each category's total amount and percentage of overall spending. If the period contains spending in multiple currencies and no specific account is supplied, the result defaults to the dominant spend currency for that window so the breakdown remains coherent.")]
    public async Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetCategoryBreakdownAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
    }

    [Description("Gets spending broken down by merchant for a given period. Returns the top merchants by total amount spent. If the period contains spending in multiple currencies and no specific account is supplied, the result defaults to the dominant spend currency for that window so the ranking remains coherent.")]
    public async Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        [Description("Number of top merchants to return (default: 10)")] int top = 10,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetMerchantBreakdownAsync(periodStart, periodEnd, personalAccountId, top, cancellationToken);
    }

    [Description("Gets spending broken down by personal account for a given period. Returns each account's total expense amount and transaction count, sorted by amount. Use this for 'which account has my biggest spend' or per-account expense comparisons.")]
    public async Task<IReadOnlyList<AccountSpendingItemResponse>> GetAccountBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetAccountBreakdownAsync(periodStart, periodEnd, cancellationToken);
    }

    [Description("Gets the all-time spend history with a specific merchant. Returns transaction count, average spend, and total spent for that merchant (already formatted with the merchant's transaction currency symbol). Use this for 'how much have I spent at <merchant>' or 'how often do I shop at <merchant>' questions.")]
    public async Task<MerchantHistoryResponse> GetMerchantHistory(
        [Description("The merchant name to look up (exact match, case-sensitive)")] string merchantName,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetMerchantHistoryAsync(merchantName, cancellationToken);
    }

    // ── Budget Mutating Tools ─────────────────────────────────────

    [Description("Adds a new budget line to the current month's budget. Pass a categoryId from the known template set (e.g. 'groceries', 'housing', 'transport', 'utilities', 'eating-out', 'bills', 'subscriptions', 'entertainment', 'savings', 'health', 'travel') when possible; leave null for a generic line. The new line starts with a zero allocation — use pf_update_budget_amount to set the limit. Requires confirmAction approval.")]
    public async Task<BudgetCategoryResponse> CreateBudget(
        [Description("Optional template category ID (e.g. 'groceries'). Null creates an unnamed/generic line.")] string? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBudgetRequest(categoryId);
        return await _budgetService.CreateBudgetAsync(request, cancellationToken);
    }

    [Description("Updates the allocated limit for a budget line in the current month. Pass the budget line ID (from pf_list_budgets line-items) and the new total allocation. Returns the refreshed budget list. Requires confirmAction approval.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> UpdateBudgetAmount(
        [Description("The unique identifier (GUID) of the budget line to update")] Guid budgetLineId,
        [Description("The new total allocation for this budget line (in the budget's currency)")] decimal totalAllocated,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateBudgetAmountRequest(totalAllocated);
        return await _budgetService.UpdateBudgetAmountAsync(budgetLineId, request, cancellationToken);
    }

    [Description("Permanently removes a budget line from the current month's budget. This is a hard delete — the line and its allocation are gone. Returns the refreshed budget list. Requires confirmAction approval.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> DeleteBudget(
        [Description("The unique identifier (GUID) of the budget line to delete")] Guid budgetLineId,
        CancellationToken cancellationToken = default)
    {
        return await _budgetService.DeleteBudgetAsync(budgetLineId, cancellationToken);
    }
}
