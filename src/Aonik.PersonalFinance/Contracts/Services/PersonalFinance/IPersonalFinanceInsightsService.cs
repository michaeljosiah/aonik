using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IPersonalFinanceInsightsService
{
    Task<SpendingSummaryResponse> GetSpendingSummaryAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        int top = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSpendingItemResponse>> GetAccountBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    Task<MerchantHistoryResponse> GetMerchantHistoryAsync(
        string merchantName,
        CancellationToken cancellationToken = default);
}
