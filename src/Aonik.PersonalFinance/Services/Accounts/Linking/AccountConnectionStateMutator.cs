using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Pure entity mutators for account-connection lifecycle state transitions.
/// Centralises the way <see cref="AccountConnection"/> and its dependent
/// <see cref="Account"/> rows shift between "Connected", "ActionRequired",
/// and "Disconnected" so every caller produces consistent state.
/// </summary>
internal static class AccountConnectionStateMutator
{
    public static void ApplyLocalDisconnect(
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        DateTime utcNow,
        string syncStatus)
    {
        connection.Status = "Disconnected";
        connection.ConsentStatus = "Revoked";
        connection.LastSyncStatus = syncStatus;
        connection.LastError = null;
        connection.DisconnectedAt = utcNow;
        connection.NextScheduledSyncAt = null;

        foreach (var linkedAccount in linkedAccounts)
        {
            linkedAccount.Status = "Archived";
            linkedAccount.LastSyncStatus = syncStatus;
            linkedAccount.LastError = null;
        }
    }

    public static void ApplyActionRequired(
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        string syncStatus,
        string message)
    {
        connection.Status = "ActionRequired";
        connection.ConsentStatus = "ActionRequired";
        connection.LastSyncStatus = syncStatus;
        connection.LastError = AccountLinkingNormalization.LimitText(message, 1000);
        connection.NextScheduledSyncAt = null;

        foreach (var linkedAccount in linkedAccounts)
        {
            linkedAccount.Status = "ActionRequired";
            linkedAccount.LastSyncStatus = syncStatus;
            linkedAccount.LastError = AccountLinkingNormalization.LimitText(message, 1000);
        }
    }

    public static void UpdateFromProviderState(
        AccountConnection connection,
        AccountLinkProviderExchangeResult providerState)
    {
        connection.ProviderConnectionReference = providerState.ProviderConnectionReference;
        if (!string.IsNullOrWhiteSpace(providerState.InstitutionName))
        {
            connection.InstitutionName = providerState.InstitutionName;
        }

        if (!string.IsNullOrWhiteSpace(providerState.InstitutionReference))
        {
            connection.InstitutionReference = providerState.InstitutionReference;
        }

        connection.Status = AccountLinkingNormalization.DetermineConnectionStatus(providerState);
        connection.ConsentStatus = providerState.ConsentStatus;
        connection.SecretReference = providerState.SecretReference;
        connection.LastSyncedAt = providerState.LastSyncedAt;
        connection.LastSyncStatus = providerState.LastSyncStatus;
        connection.LastError = AccountLinkingNormalization.DetermineConnectionError(providerState);
        connection.DisconnectedAt = null;
    }
}
