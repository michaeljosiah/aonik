using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IFinancialContextService
{
    Task<FinancialContextResponse> CreateContextAsync(
        CreateFinancialContextRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialContextResponse>> ListContextsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<FinancialContextResponse?> GetContextAsync(
        Guid contextId,
        CancellationToken cancellationToken = default);

    Task<FinancialContextResponse> UpdateContextAsync(
        Guid contextId,
        UpdateFinancialContextRequest request,
        CancellationToken cancellationToken = default);

    Task ArchiveContextAsync(
        Guid contextId,
        CancellationToken cancellationToken = default);

    Task<FinancialContextFundingSourceResponse> AddFundingSourceAsync(
        Guid contextId,
        AddFundingSourceRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveFundingSourceAsync(
        Guid contextId,
        Guid fundingSourceId,
        CancellationToken cancellationToken = default);

    Task AssignTransactionContextAsync(
        Guid transactionId,
        AssignTransactionContextRequest request,
        CancellationToken cancellationToken = default);

    Task<FinancialContextSummaryResponse> GetContextSummaryAsync(
        Guid contextId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
