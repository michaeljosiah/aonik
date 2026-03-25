namespace Aonik.Finance.Contracts.Models.PersonalFinance;

public record CreateBudgetRequest(string? CategoryId);

public record UpdateBudgetAmountRequest(decimal TotalAllocated);

public record BudgetCategoryResponse(
    string Id,
    string Name,
    string? Description,
    int IconCodePoint,
    string IconFontFamily,
    string AccentRole,
    string? LinkedSpendingCategoryId,
    IReadOnlyList<BudgetLineItemResponse> LineItems,
    IReadOnlyList<BudgetHistoryPointResponse> History);

public record BudgetLineItemResponse(
    string Id,
    string Name,
    decimal Allocated,
    decimal Spent);

public record BudgetHistoryPointResponse(
    string Label,
    decimal Amount,
    bool IsCurrent);
