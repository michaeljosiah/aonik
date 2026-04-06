using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IPersonalAccountService
{
    Task<PersonalAccountResponse> CreateAccountAsync(
        CreatePersonalAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalAccountResponse>> ListAccountsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<PersonalAccountResponse?> GetAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<PersonalAccountResponse> UpdateAccountAsync(
        Guid accountId,
        UpdatePersonalAccountRequest request,
        CancellationToken cancellationToken = default);

    Task ArchiveAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task DeleteManualAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
