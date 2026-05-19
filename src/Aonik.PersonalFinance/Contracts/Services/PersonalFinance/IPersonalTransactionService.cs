using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IPersonalTransactionService
{
    Task<PersonalTransactionResponse> CreateManualTransactionAsync(
        CreateManualPersonalTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalTransactionResponse>> ListTransactionsAsync(
        ListPersonalTransactionsRequest request,
        CancellationToken cancellationToken = default);

    Task<PersonalTransactionResponse?> GetTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<PersonalTransactionResponse> UpdateManualTransactionAsync(
        Guid transactionId,
        UpdateManualPersonalTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteManualTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);
}
