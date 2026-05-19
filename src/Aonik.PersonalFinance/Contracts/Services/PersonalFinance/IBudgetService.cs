using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetCategoryResponse>> ListBudgetsAsync(
        CancellationToken cancellationToken = default);

    Task<BudgetCategoryResponse> CreateBudgetAsync(
        CreateBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetCategoryResponse>> UpdateBudgetAmountAsync(
        Guid budgetLineId,
        UpdateBudgetAmountRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetCategoryResponse>> DeleteBudgetAsync(
        Guid budgetLineId,
        CancellationToken cancellationToken = default);
}
