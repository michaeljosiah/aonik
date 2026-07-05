using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

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

    Task<PersonalAccountResponse> ShareAccountWithHouseholdAsync(
        Guid accountId,
        ShareAccountWithHouseholdRequest request,
        CancellationToken cancellationToken = default);

    Task<PersonalAccountResponse> UnshareAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalAccountResponse>> ListHouseholdAccountsAsync(
        CancellationToken cancellationToken = default);
}
