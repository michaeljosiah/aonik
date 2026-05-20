using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Accounts;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Accounts.Linking;

/// <summary>
/// Pure DTO mappers for account-link contracts. No business logic — every
/// member is a structural projection from an entity (or party-account result)
/// onto its public response shape.
/// </summary>
internal static class AccountConnectionResponseMapper
{
    public static AccountConnectionResponse MapConnection(
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        string providerDisplayName)
    {
        return new AccountConnectionResponse(
            connection.Id,
            connection.Provider,
            providerDisplayName,
            connection.InstitutionName,
            connection.InstitutionReference,
            connection.Status,
            connection.ConsentStatus,
            connection.AutoSyncEnabled,
            connection.LastSyncedAt,
            connection.LastSyncStatus,
            connection.LastError,
            connection.DisconnectedAt,
            linkedAccounts
                .Where(item => item.AccountConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .Select(MapLinkedAccount)
                .ToList(),
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    public static LinkedAccountResponse MapLinkedAccount(Account linkedAccount)
    {
        return new LinkedAccountResponse(
            linkedAccount.Id,
            linkedAccount.Id,
            linkedAccount.Name,
            linkedAccount.AccountType,
            linkedAccount.AccountSubtype,
            linkedAccount.Currency,
            linkedAccount.MaskedIdentifier,
            linkedAccount.Status,
            linkedAccount.LastSyncedAt,
            linkedAccount.LastSyncStatus,
            linkedAccount.LastError,
            linkedAccount.CreatedAt,
            linkedAccount.UpdatedAt);
    }

    public static AccountLinkSessionResponse MapSession(
        AccountConnectionSession session,
        string providerDisplayName)
    {
        return new AccountLinkSessionResponse(
            session.Id,
            session.Provider,
            providerDisplayName,
            session.Mode,
            session.Status,
            session.AccountConnectionId,
            session.SessionToken,
            session.ExpiresAt,
            session.CreatedAt,
            session.UpdatedAt);
    }

    public static AccountResponse MapAccount(PartyAccountResult result)
    {
        return new AccountResponse(
            result.Id,
            result.AccountType,
            result.MaskedIdentifier,
            result.ProviderRef,
            result.VerificationStatus,
            result.Currency,
            result.Country,
            result.CreatedAt,
            result.UpdatedAt);
    }

    public static AccountTransactionResponse MapTransaction(AccountTransaction tx)
    {
        return new AccountTransactionResponse(
            tx.Id,
            tx.AccountId,
            tx.AccountConnectionId,
            tx.OccurredAt,
            tx.Amount,
            tx.Currency,
            tx.Counterparty,
            tx.Description,
            tx.Reference,
            tx.Category,
            tx.SubCategory,
            tx.CategoryMethod,
            tx.CategoryConfidence,
            tx.CategoryLockedAt,
            tx.Pending,
            tx.ReconciliationStatus,
            tx.MatchedLedgerEntryId,
            tx.MatchedPayoutId,
            tx.ReconciledAt,
            tx.CreatedAt,
            tx.UpdatedAt);
    }
}
