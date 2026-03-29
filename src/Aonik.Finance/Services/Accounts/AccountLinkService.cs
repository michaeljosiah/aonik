using System.Text.Json;

using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.Accounts;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Accounts;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Accounts;

internal sealed class AccountLinkService : IAccountLinkService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;
    private readonly AccountTransactionSyncOrchestrator _transactionSyncOrchestrator;
    private readonly IPartyAccountService _partyAccountService;
    private readonly IFileStore _fileStore;
    private readonly AccountConnectionSyncOptions _syncOptions;
    private readonly ILogger<AccountLinkService> _logger;

    public AccountLinkService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways,
        AccountTransactionSyncOrchestrator transactionSyncOrchestrator,
        IPartyAccountService partyAccountService,
        IFileStore fileStore,
        IOptions<AccountConnectionSyncOptions> syncOptions,
        ILogger<AccountLinkService> logger)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _providerGateways = providerGateways;
        _transactionSyncOrchestrator = transactionSyncOrchestrator;
        _partyAccountService = partyAccountService;
        _fileStore = fileStore;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task<AccountLinkSessionResponse> CreateSessionAsync(
        CreateAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Provider, nameof(request.Provider));

        var gateway = ResolveProvider(request.Provider);
        var mode = NormalizeMode(request.Mode);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        AccountConnection? connection = null;
        if (mode == "update")
        {
            if (request.ConnectionId == null || request.ConnectionId == Guid.Empty)
            {
                throw new ArgumentException("ConnectionId is required when mode is 'update'.", nameof(request.ConnectionId));
            }

            connection = await GetTenantConnectionAsync(request.ConnectionId.Value, tenantId, cancellationToken);
            if (connection == null)
            {
                throw new InvalidOperationException("Account link connection was not found.");
            }

            if (!string.Equals(connection.Provider, gateway.ProviderCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Connection provider does not match the requested update provider.");
            }

            if (connection.DisconnectedAt != null || string.Equals(connection.Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Disconnected account links cannot be reconnected. Create a new link instead.");
            }
        }

        var session = new AccountConnectionSession
        {
            TenantId = tenantId,
            UserId = userId,
            AccountConnectionId = connection?.Id,
            Provider = gateway.ProviderCode,
            Mode = mode,
            Status = "Ready"
        };

        var providerSession = await gateway.CreateSessionAsync(
            new AccountLinkProviderSessionRequest(
                tenantId,
                userId,
                session.Id,
                connection?.Id,
                connection?.ProviderConnectionReference,
                connection?.SecretReference,
                mode,
                null, // AndroidPackageName - not applicable for admin UI
                null, // RedirectUri
                TrimNullable(request.CountryCode),
                TrimNullable(request.ClientName),
                null), // PhoneNumber
            cancellationToken);

        session.SessionToken = providerSession.LaunchToken;
        session.ProviderSessionReference = providerSession.ProviderSessionReference;
        session.ExpiresAt = providerSession.ExpiresAt;

        _financeDbContext.AccountConnectionSessions.Add(session);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapSessionToResponse(session, gateway.DisplayName);
    }

    public async Task<AccountLinkExchangeResponse> ExchangeSessionAsync(
        ExchangeAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.TemporaryCode, nameof(request.TemporaryCode));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var utcNow = DateTime.UtcNow;

        var session = await _financeDbContext.AccountConnectionSessions
            .FirstOrDefaultAsync(
                item => item.Id == request.SessionId
                    && item.TenantId == tenantId,
                cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException("Account link session not found or has expired.");
        }

        if (session.ExpiresAt <= utcNow)
        {
            throw new InvalidOperationException("Account link session has expired.");
        }

        if (string.Equals(session.Status, "Exchanged", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Account link session has already been exchanged.");
        }

        var gateway = ResolveProvider(session.Provider);
        var existingConnection = await LoadSessionTargetConnectionAsync(session, tenantId, cancellationToken);

        var providerExchange = await gateway.ExchangeSessionAsync(
            new AccountLinkProviderExchangeRequest(
                tenantId,
                userId,
                session.Id,
                existingConnection?.Id,
                existingConnection?.ProviderConnectionReference,
                session.SessionToken,
                request.TemporaryCode.Trim(),
                session.Mode),
            cancellationToken);

        var connection = await ApplyProviderSyncAsync(
            existingConnection,
            gateway.ProviderCode,
            providerExchange,
            tenantId,
            userId,
            utcNow,
            cancellationToken);

        session.Status = "Exchanged";
        session.ConsumedAt = utcNow;

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        // Trigger initial transaction sync — non-fatal on failure
        try
        {
            await _transactionSyncOrchestrator.SyncConnectionTransactionsAsync(
                tenantId,
                connection.Id,
                "initial_link",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Initial transaction sync failed for external account connection {ConnectionId}; " +
                "transactions will arrive on next scheduled sync.",
                connection.Id);
        }

        var response = await BuildConnectionResponseAsync(
            connection,
            gateway.DisplayName,
            cancellationToken);

        return new AccountLinkExchangeResponse(session.Id, response);
    }

    public async Task<IReadOnlyList<AccountConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var connectionsQuery = _financeDbContext.AccountConnections
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (!includeDisconnected)
        {
            connectionsQuery = connectionsQuery.Where(item => item.DisconnectedAt == null);
        }

        var connections = await connectionsQuery
            .OrderBy(item => item.InstitutionName)
            .ThenBy(item => item.Provider)
            .ToListAsync(cancellationToken);

        if (connections.Count == 0)
        {
            return [];
        }

        var connectionIds = connections.Select(item => item.Id).ToList();
        var linkedAccounts = await _financeDbContext.Accounts
            .AsNoTracking()
            .Where(item => item.AccountConnectionId.HasValue && connectionIds.Contains(item.AccountConnectionId.Value))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return connections
            .Select(item => MapConnectionToResponse(item, linkedAccounts, ResolveProvider(item.Provider).DisplayName))
            .ToList();
    }

    public async Task<AccountConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var utcNow = DateTime.UtcNow;

        var connection = await GetTenantConnectionAsync(connectionId, tenantId, cancellationToken);
        if (connection == null)
        {
            return null;
        }

        if (connection.DisconnectedAt != null || string.Equals(connection.Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Disconnected account links cannot be refreshed.");
        }

        var gateway = ResolveProvider(connection.Provider);
        var providerRefresh = await gateway.RefreshConnectionAsync(
            new AccountLinkProviderRefreshRequest(
                tenantId,
                userId,
                connection.Id,
                connection.ProviderConnectionReference,
                connection.SecretReference),
            cancellationToken);

        await ApplyProviderSyncAsync(
            connection,
            gateway.ProviderCode,
            providerRefresh,
            tenantId,
            userId,
            utcNow,
            cancellationToken);

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return await BuildConnectionResponseAsync(connection, gateway.DisplayName, cancellationToken);
    }

    public async Task<AccountConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var utcNow = DateTime.UtcNow;

        var connection = await GetTenantConnectionAsync(connectionId, tenantId, cancellationToken);
        if (connection == null)
        {
            return null;
        }

        var linkedAccounts = await _financeDbContext.Accounts
            .Where(item => item.TenantId == tenantId
                && item.AccountConnectionId == connection.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        if (connection.DisconnectedAt == null)
        {
            await ResolveProvider(connection.Provider).DisconnectConnectionAsync(
                new AccountLinkProviderDisconnectRequest(
                    tenantId,
                    userId,
                    connection.Id,
                    connection.ProviderConnectionReference,
                    connection.SecretReference),
                cancellationToken);

            ApplyLocalDisconnectState(connection, linkedAccounts, utcNow, "Disconnected");

            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }

        return MapConnectionToResponse(connection, linkedAccounts, ResolveProvider(connection.Provider).DisplayName);
    }

    public async Task<AccountTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _transactionSyncOrchestrator.SyncConnectionTransactionsAsync(
            tenantId,
            connectionId,
            "manual_sync",
            cancellationToken);
    }

    public async Task<PagedResult<AccountTransactionResponse>> ListTransactionsAsync(
        ListAccountTransactionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _financeDbContext.AccountTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (request.AccountId.HasValue)
        {
            query = query.Where(item => item.AccountId == request.AccountId.Value);
        }

        if (request.ConnectionId.HasValue)
        {
            query = query.Where(item => item.AccountConnectionId == request.ConnectionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ReconciliationStatus))
        {
            query = query.Where(item => item.ReconciliationStatus == request.ReconciliationStatus.Trim());
        }

        if (request.From.HasValue)
        {
            query = query.Where(item => item.OccurredAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(item => item.OccurredAt <= request.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(item => item.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AccountTransactionResponse(
                item.Id,
                item.AccountId,
                item.AccountConnectionId,
                item.OccurredAt,
                item.Amount,
                item.Currency,
                item.Counterparty,
                item.Description,
                item.Reference,
                item.Category,
                item.Pending,
                item.ReconciliationStatus,
                item.MatchedLedgerEntryId,
                item.MatchedPayoutId,
                item.ReconciledAt,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountTransactionResponse>(items, totalCount, pageNumber, pageSize);
    }

    public async Task ProcessPlaidWebhookAsync(
        PlaidAccountWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var originalTenantId = _tenantContext.TenantId;
        var originalResolutionSource = _tenantContext.ResolutionSource;

        try
        {
            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                _logger.LogWarning("External account Plaid webhook did not include item_id.");
                return;
            }

            var connection = await _financeDbContext.AccountConnections
                .FirstOrDefaultAsync(
                    item => item.Provider == "Plaid"
                        && item.ProviderConnectionReference == request.ItemId.Trim(),
                    cancellationToken);

            if (connection == null)
            {
                _logger.LogWarning("No external account connection found for Plaid item {ItemId}.", request.ItemId.Trim());
                return;
            }

            _tenantContext.TenantId = connection.TenantId;
            _tenantContext.ResolutionSource = "PlaidAccountWebhook";

            connection.LastWebhookReceivedAt = utcNow;

            var linkedAccounts = await _financeDbContext.Accounts
                .Where(item => item.AccountConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            ApplyPlaidWebhook(request, connection, linkedAccounts, utcNow);

            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _tenantContext.TenantId = originalTenantId;
            _tenantContext.ResolutionSource = originalResolutionSource;
        }
    }

    private void ApplyPlaidWebhook(
        PlaidAccountWebhookRequest request,
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        DateTime utcNow)
    {
        var webhookType = NormalizeWebhookValue(request.WebhookType);
        var webhookCode = NormalizeWebhookValue(request.WebhookCode);

        if (webhookType == "ITEM" && webhookCode == "USER_PERMISSION_REVOKED")
        {
            ApplyLocalDisconnectState(connection, linkedAccounts, utcNow, "UserPermissionRevoked");
            return;
        }

        if (webhookType == "ITEM" && (webhookCode == "PENDING_DISCONNECT" || webhookCode == "PENDING_EXPIRATION"))
        {
            var message = webhookCode == "PENDING_DISCONNECT"
                ? "Reconnect required before Plaid disconnects this linked account."
                : "Consent is about to expire. Reconnect this linked account to keep syncing.";

            ApplyActionRequiredState(connection, linkedAccounts, webhookCode, message);
            return;
        }

        if (webhookType == "ITEM" && webhookCode == "ERROR")
        {
            var errorCode = NormalizeWebhookValue(request.Error?.ErrorCode);
            if (errorCode == "ITEM_LOGIN_REQUIRED" || errorCode == "PENDING_DISCONNECT")
            {
                var message = TrimNullable(request.Error?.DisplayMessage)
                    ?? TrimNullable(request.Error?.ErrorMessage)
                    ?? "Reconnect required to restore Plaid account access.";

                ApplyActionRequiredState(connection, linkedAccounts, errorCode, message);
                return;
            }

            connection.LastSyncStatus = string.IsNullOrWhiteSpace(errorCode) ? webhookCode : errorCode;
            connection.LastError = LimitText(
                request.Error?.DisplayMessage ?? request.Error?.ErrorMessage,
                1000);
            return;
        }

        if (webhookType == "TRANSACTIONS"
            && (webhookCode == "SYNC_UPDATES_AVAILABLE"
                || webhookCode == "INITIAL_UPDATE"
                || webhookCode == "HISTORICAL_UPDATE"
                || webhookCode == "DEFAULT_UPDATE"
                || webhookCode == "TRANSACTIONS_REMOVED"))
        {
            connection.LastSyncStatus = webhookCode;
            connection.LastWebhookReceivedAt = utcNow;
            if (_syncOptions.EnableRecurringSync && connection.AutoSyncEnabled)
            {
                connection.NextScheduledSyncAt = utcNow;
            }
            if (!string.Equals(connection.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase))
            {
                connection.LastError = null;
            }

            foreach (var linkedAccount in linkedAccounts)
            {
                linkedAccount.LastSyncStatus = webhookCode;
                linkedAccount.LastSyncedAt = utcNow;
                if (!string.Equals(linkedAccount.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase))
                {
                    linkedAccount.LastError = null;
                }
            }
        }
    }

    private async Task<AccountConnection> ApplyProviderSyncAsync(
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

        UpdateConnectionState(connection, providerState);
        EnsureRecurringSyncDefaults(connection);
        connection.NextScheduledSyncAt = DetermineConnectionStatus(providerState) == "Connected"
            ? ComputeNextScheduledSyncAt(connection, providerState.LastSyncedAt ?? utcNow)
            : null;

        var linkedAccountsByReference = await _financeDbContext.Accounts
            .Where(item => item.TenantId == tenantId
                && item.AccountConnectionId == connection.Id)
            .ToDictionaryAsync(item => item.ProviderAccountReference, cancellationToken);

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

    private async Task UpsertLinkedAccountAsync(
        AccountLinkProviderAccountResult providerAccount,
        AccountConnection connection,
        IDictionary<string, Account> linkedAccountsByReference,
        Guid tenantId,
        Guid tenantPartyId,
        AccountLinkProviderExchangeResult providerExchange,
        CancellationToken cancellationToken)
    {
        var maskedIdentifier = NormalizeLast4(providerAccount.Last4) ?? providerAccount.ProviderAccountReference;
        var accountType = NormalizeAccountType(providerAccount.AccountType);

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
                AccountSubtype = TrimNullable(providerAccount.AccountSubtype),
                Currency = providerAccount.Currency.Trim().ToUpperInvariant(),
                MaskedIdentifier = NormalizeLast4(providerAccount.Last4),
                Status = providerAccount.Status,
                LastSyncedAt = providerExchange.LastSyncedAt,
                LastSyncStatus = providerExchange.LastSyncStatus,
                LastError = DetermineConnectionError(providerExchange)
            };

            _financeDbContext.Accounts.Add(linkedAccount);
            linkedAccountsByReference[providerAccount.ProviderAccountReference] = linkedAccount;
            return;
        }

        // Update existing linked account
        linkedAccount.Name = providerAccount.Name;
        linkedAccount.AccountType = accountType;
        linkedAccount.AccountSubtype = TrimNullable(providerAccount.AccountSubtype);
        linkedAccount.Currency = providerAccount.Currency.Trim().ToUpperInvariant();
        linkedAccount.MaskedIdentifier = NormalizeLast4(providerAccount.Last4);
        linkedAccount.Status = providerAccount.Status;
        linkedAccount.LastSyncedAt = providerExchange.LastSyncedAt;
        linkedAccount.LastSyncStatus = providerExchange.LastSyncStatus;
        linkedAccount.LastError = DetermineConnectionError(providerExchange);
    }

    private async Task<Guid> ResolveTenantPartyIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The tenant's own party is typically the first party in the tenant.
        // Look up via the Parties read model.
        var tenantParty = await _financeDbContext.Parties
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantParty == Guid.Empty)
        {
            throw new InvalidOperationException("Could not resolve the tenant's party for external account creation.");
        }

        return tenantParty;
    }

    private static void ApplyLocalDisconnectState(
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

    private static void ApplyActionRequiredState(
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        string syncStatus,
        string message)
    {
        connection.Status = "ActionRequired";
        connection.ConsentStatus = "ActionRequired";
        connection.LastSyncStatus = syncStatus;
        connection.LastError = LimitText(message, 1000);
        connection.NextScheduledSyncAt = null;

        foreach (var linkedAccount in linkedAccounts)
        {
            linkedAccount.Status = "ActionRequired";
            linkedAccount.LastSyncStatus = syncStatus;
            linkedAccount.LastError = LimitText(message, 1000);
        }
    }

    private static void UpdateConnectionState(
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

        connection.Status = DetermineConnectionStatus(providerState);
        connection.ConsentStatus = providerState.ConsentStatus;
        connection.SecretReference = providerState.SecretReference;
        connection.LastSyncedAt = providerState.LastSyncedAt;
        connection.LastSyncStatus = providerState.LastSyncStatus;
        connection.LastError = DetermineConnectionError(providerState);
        connection.DisconnectedAt = null;
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

    private async Task<AccountConnectionResponse> BuildConnectionResponseAsync(
        AccountConnection connection,
        string providerDisplayName,
        CancellationToken cancellationToken)
    {
        var linkedAccounts = await _financeDbContext.Accounts
            .AsNoTracking()
            .Where(item => item.AccountConnectionId == connection.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return MapConnectionToResponse(connection, linkedAccounts, providerDisplayName);
    }

    private async Task<AccountConnection?> LoadSessionTargetConnectionAsync(
        AccountConnectionSession session,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (session.AccountConnectionId == null)
        {
            return null;
        }

        var connection = await GetTenantConnectionAsync(session.AccountConnectionId.Value, tenantId, cancellationToken);
        if (connection == null)
        {
            throw new InvalidOperationException("The requested account link connection could not be found for this session.");
        }

        return connection;
    }

    private Task<AccountConnection?> GetTenantConnectionAsync(
        Guid connectionId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return _financeDbContext.AccountConnections
            .FirstOrDefaultAsync(
                item => item.Id == connectionId
                    && item.TenantId == tenantId,
                cancellationToken);
    }

    private static AccountConnectionResponse MapConnectionToResponse(
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
                .Select(MapLinkedAccountToResponse)
                .ToList(),
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    private static LinkedAccountResponse MapLinkedAccountToResponse(Account linkedAccount)
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

    private static AccountLinkSessionResponse MapSessionToResponse(
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

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private IPersonalAccountLinkProviderGateway ResolveProvider(string provider)
    {
        var normalized = provider.Trim();

        var gateway = _providerGateways.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (gateway == null)
        {
            throw new ArgumentException($"Unsupported account-link provider '{provider}'.", nameof(provider));
        }

        return gateway;
    }

    private static string DetermineConnectionStatus(AccountLinkProviderExchangeResult providerState)
    {
        return !string.IsNullOrWhiteSpace(DetermineConnectionError(providerState))
            ? "ActionRequired"
            : "Connected";
    }

    private static string? DetermineConnectionError(AccountLinkProviderExchangeResult providerState)
    {
        if (!string.IsNullOrWhiteSpace(providerState.LastError))
        {
            return providerState.LastError;
        }

        if (string.Equals(providerState.ConsentStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase))
        {
            return "Reconnect required to continue syncing this account link.";
        }

        return null;
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "connect" : mode.Trim().ToLowerInvariant();

        return normalized switch
        {
            "connect" => "connect",
            "update" => "update",
            _ => throw new ArgumentException("Mode must be either 'connect' or 'update'.", nameof(mode))
        };
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static string NormalizeAccountType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "bank" => "BankAccount",
            "depository" => "BankAccount",
            "credit" => "CreditCard",
            "creditcard" => "CreditCard",
            "credit_card" => "CreditCard",
            "loan" => "Loan",
            "investment" => "Investment",
            _ => value.Trim()
        };
    }

    private static string? TrimNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        return normalized == null ? null
            : normalized.Length <= maxLength ? normalized
            : normalized[..maxLength];
    }

    private static string? NormalizeLast4(string? value)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= 4 ? normalized : normalized[^4..];
    }

    private static string NormalizeWebhookValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    // ── Manual Account CRUD ──────────────────────────────────────

    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Name, nameof(request.Name));
        ValidateRequiredText(request.AccountType, nameof(request.AccountType));
        ValidateRequiredText(request.Currency, nameof(request.Currency));

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var tenantPartyId = await ResolveTenantPartyIdAsync(tenantId, cancellationToken);

        var maskedIdentifier = NormalizeLast4(request.Last4) ?? request.Name.Trim();
        var metadataJson = BuildAccountMetadataJson(request);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant();
        var country = TrimNullable(request.Country);

        var result = await _partyAccountService.CreatePartyAccountAsync(
            tenantId,
            tenantPartyId,
            request.AccountType.Trim(),
            maskedIdentifier,
            null,
            "Manual",
            currency,
            country,
            metadataJson,
            cancellationToken);

        return MapAccountToResponse(result);
    }

    public async Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var results = await _partyAccountService.ListPartyAccountsAsync(tenantId, cancellationToken);

        return results.Select(MapAccountToResponse).ToList();
    }

    // ── Manual Transaction CRUD ──────────────────────────────────

    public async Task<AccountTransactionResponse> CreateTransactionAsync(
        CreateAccountTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount == 0)
        {
            throw new ArgumentException("Amount cannot be zero.", nameof(request.Amount));
        }

        ValidateRequiredText(request.Currency, nameof(request.Currency));

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Verify account exists for this tenant
        var existingAccount = await _partyAccountService.GetPartyAccountAsync(
            tenantId,
            request.AccountId,
            cancellationToken);

        if (existingAccount == null)
        {
            throw new InvalidOperationException($"Account {request.AccountId} not found.");
        }

        var transaction = new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = request.AccountId,
            AccountConnectionId = null,
            ProviderTransactionReference = $"manual-{Guid.NewGuid():N}",
            OccurredAt = request.OccurredAt,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Counterparty = TrimNullable(request.Counterparty),
            Description = TrimNullable(request.Description),
            Reference = TrimNullable(request.Reference),
            Category = TrimNullable(request.Category),
            Pending = false,
            ReconciliationStatus = "Unmatched",
            Notes = TrimNullable(request.Notes)
        };

        _financeDbContext.AccountTransactions.Add(transaction);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return new AccountTransactionResponse(
            transaction.Id,
            transaction.AccountId,
            transaction.AccountConnectionId,
            transaction.OccurredAt,
            transaction.Amount,
            transaction.Currency,
            transaction.Counterparty,
            transaction.Description,
            transaction.Reference,
            transaction.Category,
            transaction.Pending,
            transaction.ReconciliationStatus,
            transaction.MatchedLedgerEntryId,
            transaction.MatchedPayoutId,
            transaction.ReconciledAt,
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }

    // ── Transaction Attachments ──────────────────────────────────

    public async Task<AccountTransactionAttachmentResponse> AddTransactionAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transactionExists = await _financeDbContext.AccountTransactions
            .AnyAsync(t => t.Id == transactionId && t.TenantId == tenantId, cancellationToken);

        if (!transactionExists)
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found.");
        }

        var uploadResult = await _fileStore.UploadAsync(
            tenantId,
            transactionId,
            fileStream,
            fileName,
            contentType,
            cancellationToken);

        var attachment = new AccountTransactionAttachment
        {
            TenantId = tenantId,
            TransactionId = transactionId,
            StorageProvider = uploadResult.StorageProvider,
            StorageContainer = uploadResult.StorageContainer,
            StorageKey = uploadResult.StorageKey,
            ContentType = uploadResult.ContentType,
            FileName = uploadResult.FileName,
            FileSizeBytes = uploadResult.FileSizeBytes,
            Sha256 = uploadResult.Sha256
        };

        _financeDbContext.AccountTransactionAttachments.Add(attachment);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapAttachmentToResponse(attachment);
    }

    public async Task<IReadOnlyList<AccountTransactionAttachmentResponse>> ListTransactionAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var attachments = await _financeDbContext.AccountTransactionAttachments
            .AsNoTracking()
            .Where(a => a.TransactionId == transactionId && a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapAttachmentToResponse).ToList();
    }

    public async Task DeleteTransactionAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var attachment = await _financeDbContext.AccountTransactionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId, cancellationToken);

        if (attachment == null)
        {
            throw new InvalidOperationException($"Attachment {attachmentId} not found.");
        }

        try
        {
            await _fileStore.DeleteAsync(attachment.StorageKey, cancellationToken);
        }
        catch
        {
            // Best-effort blob deletion; entity removal is authoritative
        }

        _financeDbContext.AccountTransactionAttachments.Remove(attachment);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    private AccountTransactionAttachmentResponse MapAttachmentToResponse(
        AccountTransactionAttachment attachment)
    {
        return new AccountTransactionAttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            _fileStore.GetUrl(attachment.StorageKey),
            attachment.FileSizeBytes,
            attachment.CreatedAt);
    }

    private static AccountResponse MapAccountToResponse(PartyAccountResult result)
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

    private static string BuildAccountMetadataJson(CreateAccountRequest request)
    {
        var metadata = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(request.Name))
            metadata["name"] = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Currency))
            metadata["currency"] = request.Currency.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.InstitutionName))
            metadata["institutionName"] = request.InstitutionName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Notes))
            metadata["notes"] = request.Notes.Trim();

        return JsonSerializer.Serialize(metadata);
    }
}
