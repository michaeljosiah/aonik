using Aonik.Application.Models.Pricing;

namespace Aonik.Application.Services.Pricing;

public interface IFxQuoteService
{
    Task<IReadOnlyCollection<FxQuoteListResponse>> GetAllAsync(
        string? baseCurrency = null,
        string? targetCurrency = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default);

    Task<FxQuoteDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FxQuoteDetailResponse> CreateAsync(
        CreateFxQuoteRequest request,
        CancellationToken cancellationToken = default);

    Task<FxQuoteDetailResponse> UpdateAsync(
        Guid id,
        UpdateFxQuoteRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
