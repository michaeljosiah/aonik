using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Applies a provider exchange/refresh result to the local account-link
/// state: upserts the <see cref="AccountConnection"/>, propagates the new
/// state, and reconciles the linked <see cref="Account"/> rows. Also owns
/// the recurring-sync schedule arithmetic and tenant-party lookup needed
/// for new account creation.
/// </summary>
internal sealed class AccountConnectionSyncApplicator
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly IPartyAccountService _partyAccountService;
    private readonly IPartyReader _partyReader;
    private readonly AccountConnectionSyncOptions _syncOptions;

    public AccountConnectionSyncApplicator(
        PersonalFinanceDbContext financeDbContext,
        IPartyAccountService partyAccountService,
        IPartyReader partyReader,
        IOptions<AccountConnectionSyncOptions> syncOptions)
    {
        _financeDbContext = financeDbContext;
        _partyAccountService = partyAccountService;
        _partyReader = partyReader;
        _syncOptions = syncOptions.Value;
    }

    public async Task<AccountConnection> ApplyProviderSyncAsync(
        AccountConnection? existingConnection,
        string providerCode,
        AccountLinkProviderExchangeResult providerState,
        Guid tenantId,
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var connection = existingConnection;
        if (connection == null)
        {
            connection = await _financeDbContext.AccountConnections
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId
                        && item.Provider == providerCode
                        && item.ProviderConnectionReference == providerState.ProviderConnectionReference,
                    cancellationToken);
        }

        if (connection == null)
        {
            connection = new AccountConnection
            {
                TenantId = tenantId,
                CreatedByUserId = userId,
                Provider = providerCode,
                AutoSyncEnabled = true,
                SyncIntervalMinutes = Math.Max(_syncOptions.DefaultSyncIntervalMinutes, 1)
            };

            _financeDbContext.AccountConnections.Add(connection);
        }

        AccountConnectionStateMutator.UpdateFromProviderState(connection, providerState);
        EnsureRecurringSyncDefaults(connection);
        connection.NextScheduledSyncAt = AccountLinkingNormalization.DetermineConnectionStatus(providerState) == "Connected"
            ? ComputeNextScheduledSyncAt(connection, providerState.LastSyncedAt ?? utcNow)
            : null;

        var linkedAccountsByReference = await _financeDbContext.Accounts
            .Where(item => item.TenantId == tenantId
                && item.AccountConnectionId == connection.Id
                && item.ProviderAccountReference != null)
            .ToDictionaryAsync(item => item.ProviderAccountReference!, cancellationToken);

        // Resolve the tenant's own party ID for Account creation
        var tenantPartyId = await ResolveTenantPartyIdAsync(tenantId, cancellationToken);

        foreach (var providerAccount in providerState.Accounts)
        {
            await UpsertLinkedAccountAsync(
                providerAccount,
                connection,
                linkedAccountsByReference,
                tenantId,
                tenantPartyId,
                providerState,
                cancellationToken);
        }

        return connection;
    }

    public async Task<AccountConnectionResponse> BuildConnectionResponseAsync(
        AccountConnection connection,
        string providerDisplayName,
        CancellationToken cancellationToken)
    {
        var linkedAccounts = await _financeDbContext.Accounts
            .AsNoTracking()
            .Where(item => item.AccountConnectionId == connection.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return AccountConnectionResponseMapper.MapConnection(connection, linkedAccounts, providerDisplayName);
    }

    public async Task<Guid> ResolveTenantPartyIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The tenant's own party is typically the first party in the tenant.
        // Resolved through the SharedKernel Platform read contract so this
        // slice no longer touches the Finance-resident Parties read model.
        var tenantParty = await _partyReader.GetTenantPartyIdAsync(tenantId, cancellationToken);

        if (tenantParty is null || tenantParty.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Could not resolve the tenant's party for external account creation.");
        }

        return tenantParty.Value;
    }

    private async Task UpsertLinkedAccountAsync(
        AccountLinkProviderAccountResult providerAccount,
        AccountConnection connection,
        IDictionary<string, Account> linkedAccountsByReference,
        Guid tenantId,
        Guid tenantPartyId,
        AccountLinkProviderExchangeResult providerExchange,
        CancellationToken cancellationToken)
    {
        var maskedIdentifier = AccountLinkingNormalization.NormalizeLast4(providerAccount.Last4) ?? providerAccount.ProviderAccountReference;
        var accountType = AccountLinkingNormalization.NormalizeAccountType(providerAccount.AccountType);

        if (!linkedAccountsByReference.TryGetValue(providerAccount.ProviderAccountReference, out var linkedAccount))
        {
            // Find or create the Account in Platform
            var externalAccountId = await _partyAccountService.FindOrCreatePartyAccountAsync(
                tenantId,
                tenantPartyId,
                accountType,
                maskedIdentifier,
                providerAccount.ProviderAccountReference,
                cancellationToken);

            linkedAccount = new Account
            {
                TenantId = tenantId,
                AccountConnectionId = connection.Id,
                ProviderAccountReference = providerAccount.ProviderAccountReference,
                Name = providerAccount.Name,
                AccountType = accountType,
                AccountSubtype = AccountLinkingNormalization.TrimNullable(providerAccount.AccountSubtype),
                Currency = providerAccount.Currency.Trim().ToUpperInvariant(),
                MaskedIdentifier = AccountLinkingNormalization.NormalizeLast4(providerAccount.Last4),
                Status = providerAccount.Status,
                LastSyncedAt = providerExchange.LastSyncedAt,
                LastSyncStatus = providerExchange.LastSyncStatus,
                LastError = AccountLinkingNormalization.DetermineConnectionError(providerExchange)
            };

            _financeDbContext.Accounts.Add(linkedAccount);
            linkedAccountsByReference[providerAccount.ProviderAccountReference] = linkedAccount;
            return;
        }

        // Update existing linked account
        linkedAccount.Name = providerAccount.Name;
        linkedAccount.AccountType = accountType;
        linkedAccount.AccountSubtype = AccountLinkingNormalization.TrimNullable(providerAccount.AccountSubtype);
        linkedAccount.Currency = providerAccount.Currency.Trim().ToUpperInvariant();
        linkedAccount.MaskedIdentifier = AccountLinkingNormalization.NormalizeLast4(providerAccount.Last4);
        linkedAccount.Status = providerAccount.Status;
        linkedAccount.LastSyncedAt = providerExchange.LastSyncedAt;
        linkedAccount.LastSyncStatus = providerExchange.LastSyncStatus;
        linkedAccount.LastError = AccountLinkingNormalization.DetermineConnectionError(providerExchange);
    }

    private void EnsureRecurringSyncDefaults(AccountConnection connection)
    {
        if (connection.SyncIntervalMinutes <= 0)
        {
            connection.SyncIntervalMinutes = Math.Max(_syncOptions.DefaultSyncIntervalMinutes, 1);
        }
    }

    private DateTime? ComputeNextScheduledSyncAt(AccountConnection connection, DateTime fromUtc)
    {
        if (!_syncOptions.EnableRecurringSync || !connection.AutoSyncEnabled)
        {
            return null;
        }

        var intervalMinutes = connection.SyncIntervalMinutes > 0
            ? connection.SyncIntervalMinutes
            : Math.Max(_syncOptions.DefaultSyncIntervalMinutes, 1);

        return fromUtc.AddMinutes(intervalMinutes);
    }
}
