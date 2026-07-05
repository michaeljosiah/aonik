using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.PersonalFinance.Services;

internal sealed class PersonalAccountLinkService : IPersonalAccountLinkService
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;
    private readonly FinancialConnectionTransactionSyncOrchestrator _transactionSyncOrchestrator;
    private readonly FinancialConnectionSyncOptions _syncOptions;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;
    private readonly ILogger<PersonalAccountLinkService> _logger;

    public PersonalAccountLinkService(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways,
        FinancialConnectionTransactionSyncOrchestrator transactionSyncOrchestrator,
        Microsoft.Extensions.Options.IOptions<FinancialConnectionSyncOptions> syncOptions,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator,
        ILogger<PersonalAccountLinkService> logger)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _providerGateways = providerGateways;
        _transactionSyncOrchestrator = transactionSyncOrchestrator;
        _syncOptions = syncOptions.Value;
        _cacheInvalidator = cacheInvalidator;
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

        FinancialConnection? connection = null;
        if (mode == "update")
        {
            if (request.ConnectionId == null || request.ConnectionId == Guid.Empty)
            {
                throw new ArgumentException("ConnectionId is required when mode is 'update'.", nameof(request.ConnectionId));
            }

            connection = await GetOwnedConnectionAsync(request.ConnectionId.Value, tenantId, userId, cancellationToken);
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

        var session = new FinancialConnectionSession
        {
            TenantId = tenantId,
            UserId = userId,
            FinancialConnectionId = connection?.Id,
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
                TrimNullable(request.AndroidPackageName),
                TrimNullable(request.RedirectUri),
                TrimNullable(request.CountryCode),
                TrimNullable(request.ClientName),
                TrimNullable(request.PhoneNumber)),
            cancellationToken);

        session.SessionToken = providerSession.LaunchToken;
        session.ProviderSessionReference = providerSession.ProviderSessionReference;
        session.ExpiresAt = providerSession.ExpiresAt;

        _financeDbContext.FinancialConnectionSessions.Add(session);
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

        var session = await _financeDbContext.FinancialConnectionSessions
            .FirstOrDefaultAsync(
                item => item.Id == request.AccountLinkSessionId
                    && item.TenantId == tenantId
                    && item.UserId == userId,
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
        var existingConnection = await LoadSessionTargetConnectionAsync(session, tenantId, userId, cancellationToken);

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
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        // Trigger initial transaction sync so transactions are available immediately
        // after linking. Failures are non-fatal — transactions will arrive on the
        // next scheduled sync or via provider webhook.
        try
        {
            await _transactionSyncOrchestrator.SyncConnectionTransactionsAsync(
                tenantId,
                userId,
                connection.Id,
                "initial_link",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Initial transaction sync failed for connection {ConnectionId}; " +
                "transactions will arrive on next scheduled sync.",
                connection.Id);
        }

        var response = await BuildConnectionResponseAsync(
            connection,
            gateway.DisplayName,
            cancellationToken);

        return new AccountLinkExchangeResponse(session.Id, response);
    }

    public async Task<IReadOnlyList<AccountLinkConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var connectionsQuery = _financeDbContext.FinancialConnections
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId);

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
        var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
            .AsNoTracking()
            .Where(item => connectionIds.Contains(item.FinancialConnectionId))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return connections
            .Select(item => MapConnectionToResponse(item, linkedAccounts, ResolveProvider(item.Provider).DisplayName))
            .ToList();
    }

    public async Task<AccountLinkConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var utcNow = DateTime.UtcNow;

        var connection = await GetOwnedConnectionAsync(connectionId, tenantId, userId, cancellationToken);
        if (connection == null)
        {
            return null;
        }

        if (connection.DisconnectedAt != null || string.Equals(connection.Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Disconnected account links cannot be refreshed.");
        }

        if (string.Equals(connection.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.ConsentStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateActionRequiredException(connection);
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
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        if (string.Equals(connection.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.ConsentStatus, "ActionRequired", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateActionRequiredException(connection);
        }

        return await BuildConnectionResponseAsync(connection, gateway.DisplayName, cancellationToken);
    }

    public async Task<AccountLinkConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var utcNow = DateTime.UtcNow;

        var connection = await GetOwnedConnectionAsync(connectionId, tenantId, userId, cancellationToken);
        if (connection == null)
        {
            return null;
        }

        var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
            .Where(item => item.TenantId == tenantId
                && item.UserId == userId
                && item.FinancialConnectionId == connection.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var personalAccountIds = linkedAccounts.Select(item => item.PersonalAccountId).ToList();
        var personalAccounts = personalAccountIds.Count == 0
            ? []
            : await _financeDbContext.PersonalAccounts
                .Where(item => item.TenantId == tenantId
                    && item.UserId == userId
                    && personalAccountIds.Contains(item.Id))
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

            ApplyLocalDisconnectState(connection, linkedAccounts, personalAccounts, utcNow, "Disconnected");

            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
        }

        return MapConnectionToResponse(connection, linkedAccounts, ResolveProvider(connection.Provider).DisplayName);
    }

    public async Task<AccountLinkTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        return await _transactionSyncOrchestrator.SyncConnectionTransactionsAsync(
            tenantId,
            userId,
            connectionId,
            LinkedAccountSyncSourceType,
            cancellationToken);
    }

    public async Task ProcessPlaidWebhookAsync(
        PlaidAccountLinkWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var webhookEvent = new FinancialWebhookEvent
        {
            Provider = "Plaid",
            ProviderConnectionReference = TrimNullable(request.ItemId) ?? string.Empty,
            ProviderEventType = NormalizeWebhookValue(request.WebhookType),
            ProviderEventCode = NormalizeWebhookValue(request.WebhookCode),
            ProcessingStatus = "Received",
            PayloadJson = JsonSerializer.Serialize(request),
            ReceivedAt = utcNow
        };

        _financeDbContext.FinancialWebhookEvents.Add(webhookEvent);

        var originalTenantId = _tenantContext.TenantId;
        var originalResolutionSource = _tenantContext.ResolutionSource;

        try
        {
            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                webhookEvent.ProcessingStatus = "Ignored";
                webhookEvent.Error = "Plaid webhook did not include item_id.";
                webhookEvent.ProcessedAt = utcNow;
                await _financeDbContext.SaveChangesAsync(cancellationToken);
                await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
                return;
            }

            var connection = await _financeDbContext.FinancialConnections
                .FirstOrDefaultAsync(
                    item => item.Provider == "Plaid"
                        && item.ProviderConnectionReference == request.ItemId.Trim(),
                    cancellationToken);

            if (connection == null)
            {
                webhookEvent.ProcessingStatus = "Ignored";
                webhookEvent.Error = $"No financial connection found for Plaid item {request.ItemId.Trim()}.";
                webhookEvent.ProcessedAt = utcNow;
                await _financeDbContext.SaveChangesAsync(cancellationToken);
                await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
                return;
            }

            webhookEvent.TenantId = connection.TenantId;
            webhookEvent.UserId = connection.UserId;
            webhookEvent.FinancialConnectionId = connection.Id;
            connection.LastWebhookReceivedAt = utcNow;

            _tenantContext.TenantId = connection.TenantId;
            _tenantContext.ResolutionSource = "PlaidWebhook";

            var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
                .Where(item => item.FinancialConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var personalAccountIds = linkedAccounts.Select(item => item.PersonalAccountId).Distinct().ToList();
            var personalAccounts = personalAccountIds.Count == 0
                ? []
                : await _financeDbContext.PersonalAccounts
                    .Where(item => personalAccountIds.Contains(item.Id))
                    .ToListAsync(cancellationToken);

            webhookEvent.ProcessingStatus = ApplyPlaidWebhook(
                request,
                connection,
                linkedAccounts,
                personalAccounts,
                utcNow);
            webhookEvent.ProcessedAt = utcNow;

            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            webhookEvent.ProcessingStatus = "Failed";
            webhookEvent.Error = LimitText(ex.Message, 1000);
            webhookEvent.ProcessedAt = DateTime.UtcNow;

            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
            throw;
        }
        finally
        {
            _tenantContext.TenantId = originalTenantId;
            _tenantContext.ResolutionSource = originalResolutionSource;
        }
    }

    public async Task<IReadOnlyList<AccountLinkSummaryItemResponse>> GetSummaryAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var accountsQuery = _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId);

        if (!includeArchived)
        {
            accountsQuery = accountsQuery.Where(item => !item.IsArchived);
        }

        var personalAccounts = await accountsQuery
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        if (personalAccounts.Count == 0)
        {
            return [];
        }

        var personalAccountIds = personalAccounts.Select(item => item.Id).ToList();
        var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
            .AsNoTracking()
            .Where(item => personalAccountIds.Contains(item.PersonalAccountId))
            .ToListAsync(cancellationToken);

        var connectionIds = linkedAccounts.Select(item => item.FinancialConnectionId).Distinct().ToList();
        List<FinancialConnection> connections;
        if (connectionIds.Count == 0)
        {
            connections = [];
        }
        else
        {
            connections = await _financeDbContext.FinancialConnections
                .AsNoTracking()
                .Where(item => connectionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        }

        var linkedAccountsByPersonalAccountId = linkedAccounts
            .GroupBy(item => item.PersonalAccountId)
            .ToDictionary(group => group.Key, group => group.First());

        var connectionsById = connections.ToDictionary(item => item.Id);

        var summaryItems = new List<AccountLinkSummaryItemResponse>(personalAccounts.Count);
        foreach (var account in personalAccounts)
        {
            linkedAccountsByPersonalAccountId.TryGetValue(account.Id, out var linkedAccount);

            FinancialConnection? connection = null;
            if (linkedAccount != null)
            {
                connectionsById.TryGetValue(linkedAccount.FinancialConnectionId, out connection);
            }

            summaryItems.Add(MapSummaryItem(account, linkedAccount, connection));
        }

        return summaryItems;
    }

    private async Task<FinancialConnection> ApplyProviderSyncAsync(
        FinancialConnection? existingConnection,
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
            connection = await _financeDbContext.FinancialConnections
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId
                        && item.UserId == userId
                        && item.Provider == providerCode
                        && item.ProviderConnectionReference == providerState.ProviderConnectionReference,
                    cancellationToken);
        }

        if (connection == null)
        {
            connection = new FinancialConnection
            {
                TenantId = tenantId,
                UserId = userId,
                Provider = providerCode,
                AutoSyncEnabled = true,
                SyncIntervalMinutes = Math.Max(_syncOptions.DefaultSyncIntervalMinutes, 1)
            };

            _financeDbContext.FinancialConnections.Add(connection);
        }

        var previousDisconnectedAt = connection.DisconnectedAt;

        UpdateConnectionState(connection, providerState);
        EnsureRecurringSyncDefaults(connection);
        connection.NextScheduledSyncAt = DetermineConnectionStatus(providerState) == "Connected"
            ? ComputeNextScheduledSyncAt(connection, providerState.LastSyncedAt ?? utcNow)
            : null;

        var linkedAccountsByReference = await _financeDbContext.PersonalLinkedAccounts
            .Where(item => item.TenantId == tenantId
                && item.UserId == userId
                && item.FinancialConnectionId == connection.Id)
            .ToDictionaryAsync(item => item.ProviderAccountReference, cancellationToken);

        // Collect all PersonalAccount IDs already linked so we can bulk-fetch them.
        var linkedPersonalAccountIds = linkedAccountsByReference.Values
            .Select(item => item.PersonalAccountId)
            .Distinct()
            .ToList();

        // Collect provider account references that are NOT yet linked so we can
        // bulk-fetch any orphaned PersonalAccounts that share the same ExternalReference.
        var providerReferences = providerState.Accounts
            .Select(item => item.ProviderAccountReference)
            .ToList();

        var unlinkedProviderReferences = providerReferences
            .Where(r => !linkedAccountsByReference.ContainsKey(r))
            .ToList();

        // Single query: PersonalAccounts keyed by Id (covers existing-linked-account update path
        // and the error short-circuit path below).
        var personalAccountsById = linkedPersonalAccountIds.Count == 0
            ? new Dictionary<Guid, PersonalAccount>()
            : await _financeDbContext.PersonalAccounts
                .Where(item => linkedPersonalAccountIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        // Single query: PersonalAccounts keyed by ExternalReference (covers new-linked-account
        // path where a PersonalAccount may already exist for a provider reference not yet linked).
        var personalAccountsByExternalRef = unlinkedProviderReferences.Count == 0
            ? new Dictionary<string, PersonalAccount>()
            : await _financeDbContext.PersonalAccounts
                .Where(item => item.TenantId == tenantId
                    && item.UserId == userId
                    && item.ExternalReference != null
                    && unlinkedProviderReferences.Contains(item.ExternalReference))
                .ToDictionaryAsync(item => item.ExternalReference!, cancellationToken);

        if (providerState.Accounts.Count == 0 && !string.IsNullOrWhiteSpace(providerState.LastError))
        {
            foreach (var linkedAccount in linkedAccountsByReference.Values)
            {
                linkedAccount.Status = "ActionRequired";
                linkedAccount.LastSyncStatus = providerState.LastSyncStatus;
                linkedAccount.LastError = providerState.LastError;

                if (personalAccountsById.TryGetValue(linkedAccount.PersonalAccountId, out var personalAccount))
                {
                    personalAccount.Status = "ActionRequired";
                }
            }
        }

        foreach (var providerAccount in providerState.Accounts)
        {
            UpsertLinkedAccount(
                providerAccount,
                connection,
                linkedAccountsByReference,
                personalAccountsByExternalRef,
                personalAccountsById,
                previousDisconnectedAt,
                tenantId,
                userId,
                providerState,
                utcNow);
        }

        return connection;
    }

    private string ApplyPlaidWebhook(
        PlaidAccountLinkWebhookRequest request,
        FinancialConnection connection,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts,
        IReadOnlyList<PersonalAccount> personalAccounts,
        DateTime utcNow)
    {
        var webhookType = NormalizeWebhookValue(request.WebhookType);
        var webhookCode = NormalizeWebhookValue(request.WebhookCode);

        if (webhookType == "ITEM" && webhookCode == "USER_PERMISSION_REVOKED")
        {
            ApplyLocalDisconnectState(connection, linkedAccounts, personalAccounts, utcNow, "UserPermissionRevoked");
            return "Processed";
        }

        if (webhookType == "ITEM" && (webhookCode == "PENDING_DISCONNECT" || webhookCode == "PENDING_EXPIRATION"))
        {
            var message = webhookCode == "PENDING_DISCONNECT"
                ? "Reconnect required before Plaid disconnects this linked account."
                : "Consent is about to expire. Reconnect this linked account to keep syncing.";

            ProviderTransactionMapper.ApplyActionRequiredState(connection, linkedAccounts, personalAccounts, webhookCode, message);
            return "Processed";
        }

        if (webhookType == "ITEM" && webhookCode == "ERROR")
        {
            var errorCode = NormalizeWebhookValue(request.Error?.ErrorCode);
            if (errorCode == "ITEM_LOGIN_REQUIRED" || errorCode == "PENDING_DISCONNECT")
            {
                var message = ProviderTransactionMapper.LimitText(
                    request.Error?.DisplayMessage
                        ?? request.Error?.ErrorMessage
                        ?? "Reconnect required to restore Plaid account access.",
                    1000) ?? "Reconnect required to restore Plaid account access.";

                ProviderTransactionMapper.ApplyActionRequiredState(connection, linkedAccounts, personalAccounts, errorCode, message);
                return "Processed";
            }

            connection.LastSyncStatus = string.IsNullOrWhiteSpace(errorCode) ? webhookCode : errorCode;
            connection.LastError = ProviderTransactionMapper.LimitText(
                request.Error?.DisplayMessage ?? request.Error?.ErrorMessage,
                1000);
            return "Processed";
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

            return "Processed";
        }

        return "Ignored";
    }

    private static void ApplyActionRequiredState(
        FinancialConnection connection,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts,
        IReadOnlyList<PersonalAccount> personalAccounts,
        string syncStatus,
        string message)
    {
        ProviderTransactionMapper.ApplyActionRequiredState(connection, linkedAccounts, personalAccounts, syncStatus, message);
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return new Guid(bytes);
    }

    private static void ApplyLocalDisconnectState(
        FinancialConnection connection,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts,
        IReadOnlyList<PersonalAccount> personalAccounts,
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

        foreach (var personalAccount in personalAccounts)
        {
            personalAccount.Status = "Archived";
            personalAccount.IsArchived = true;
            personalAccount.ClosedAt ??= utcNow;
        }
    }

    private static void UpdateConnectionState(
        FinancialConnection connection,
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

    private void EnsureRecurringSyncDefaults(FinancialConnection connection)
    {
        if (connection.SyncIntervalMinutes <= 0)
        {
            connection.SyncIntervalMinutes = Math.Max(_syncOptions.DefaultSyncIntervalMinutes, 1);
        }
    }

    private DateTime? ComputeNextScheduledSyncAt(FinancialConnection connection, DateTime fromUtc)
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

    private static string DetermineConnectionStatus(AccountLinkProviderExchangeResult providerState)
    {
        if (!string.IsNullOrWhiteSpace(DetermineConnectionError(providerState)))
        {
            return "ActionRequired";
        }

        return "Connected";
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

    private void UpsertLinkedAccount(
        AccountLinkProviderAccountResult providerAccount,
        FinancialConnection connection,
        IDictionary<string, PersonalLinkedAccount> linkedAccountsByReference,
        IDictionary<string, PersonalAccount> personalAccountsByExternalRef,
        IDictionary<Guid, PersonalAccount> personalAccountsById,
        DateTime? previousDisconnectedAt,
        Guid tenantId,
        Guid userId,
        AccountLinkProviderExchangeResult providerExchange,
        DateTime utcNow)
    {
        if (!linkedAccountsByReference.TryGetValue(providerAccount.ProviderAccountReference, out var linkedAccount))
        {
            // No linked account yet — find or create the backing PersonalAccount.
            if (!personalAccountsByExternalRef.TryGetValue(providerAccount.ProviderAccountReference, out var personalAccount))
            {
                personalAccount = new PersonalAccount
                {
                    TenantId = tenantId,
                    UserId = userId,
                    Name = providerAccount.Name,
                    AccountType = NormalizePersonalAccountType(providerAccount.AccountType),
                    Currency = providerAccount.Currency.Trim().ToUpperInvariant(),
                    InstitutionName = providerExchange.InstitutionName,
                    ExternalReference = providerAccount.ProviderAccountReference,
                    Status = providerAccount.Status,
                    AccountSubtype = TrimNullable(providerAccount.AccountSubtype),
                    Last4 = NormalizeLast4(providerAccount.Last4),
                    IsArchived = false,
                    OpenedAt = utcNow
                };

                _financeDbContext.PersonalAccounts.Add(personalAccount);
                personalAccountsByExternalRef[providerAccount.ProviderAccountReference] = personalAccount;
            }
            else
            {
                personalAccount.Name = providerAccount.Name;
                personalAccount.AccountType = NormalizePersonalAccountType(providerAccount.AccountType);
                personalAccount.Currency = providerAccount.Currency.Trim().ToUpperInvariant();
                personalAccount.InstitutionName = providerExchange.InstitutionName;
                personalAccount.ExternalReference = providerAccount.ProviderAccountReference;
                personalAccount.AccountSubtype = TrimNullable(providerAccount.AccountSubtype);
                personalAccount.Last4 = NormalizeLast4(providerAccount.Last4);
                ProviderTransactionMapper.ApplyConnectedPersonalAccountState(personalAccount, null, previousDisconnectedAt, providerAccount.Status);
            }

            linkedAccount = new PersonalLinkedAccount
            {
                TenantId = tenantId,
                UserId = userId,
                FinancialConnectionId = connection.Id,
                PersonalAccountId = personalAccount.Id,
                ProviderAccountReference = providerAccount.ProviderAccountReference,
                Name = providerAccount.Name,
                AccountType = NormalizePersonalAccountType(providerAccount.AccountType),
                AccountSubtype = TrimNullable(providerAccount.AccountSubtype),
                Currency = providerAccount.Currency.Trim().ToUpperInvariant(),
                Last4 = NormalizeLast4(providerAccount.Last4),
                Status = providerAccount.Status,
                LastSyncedAt = providerExchange.LastSyncedAt,
                LastSyncStatus = providerExchange.LastSyncStatus,
                LastError = DetermineAccountError(providerExchange)
            };

            _financeDbContext.PersonalLinkedAccounts.Add(linkedAccount);
            linkedAccountsByReference[providerAccount.ProviderAccountReference] = linkedAccount;
            return;
        }

        // Existing linked account — update the backing PersonalAccount from the pre-fetched dictionary.
        if (!personalAccountsById.TryGetValue(linkedAccount.PersonalAccountId, out var linkedPersonalAccount))
            throw new InvalidOperationException(
                $"PersonalAccount {linkedAccount.PersonalAccountId} not found for linked account {linkedAccount.Id}.");

        linkedPersonalAccount.Name = providerAccount.Name;
        linkedPersonalAccount.AccountType = NormalizePersonalAccountType(providerAccount.AccountType);
        linkedPersonalAccount.Currency = providerAccount.Currency.Trim().ToUpperInvariant();
        linkedPersonalAccount.InstitutionName = providerExchange.InstitutionName;
        linkedPersonalAccount.ExternalReference = providerAccount.ProviderAccountReference;
        linkedPersonalAccount.AccountSubtype = TrimNullable(providerAccount.AccountSubtype);
        linkedPersonalAccount.Last4 = NormalizeLast4(providerAccount.Last4);
        ProviderTransactionMapper.ApplyConnectedPersonalAccountState(linkedPersonalAccount, linkedAccount, previousDisconnectedAt, providerAccount.Status);

        linkedAccount.Name = providerAccount.Name;
        linkedAccount.AccountType = NormalizePersonalAccountType(providerAccount.AccountType);
        linkedAccount.AccountSubtype = TrimNullable(providerAccount.AccountSubtype);
        linkedAccount.Currency = providerAccount.Currency.Trim().ToUpperInvariant();
        linkedAccount.Last4 = NormalizeLast4(providerAccount.Last4);
        linkedAccount.Status = providerAccount.Status;
        linkedAccount.LastSyncedAt = providerExchange.LastSyncedAt;
        linkedAccount.LastSyncStatus = providerExchange.LastSyncStatus;
        linkedAccount.LastError = DetermineAccountError(providerExchange);
    }

    private static string? DetermineAccountError(AccountLinkProviderExchangeResult providerExchange)
    {
        return DetermineConnectionError(providerExchange);
    }

    private static AccountLinkActionRequiredException CreateActionRequiredException(FinancialConnection connection)
    {
        var providerErrorCode = TrimNullable(connection.LastSyncStatus);
        var message = TrimNullable(connection.LastError)
            ?? "Reconnect this account link before requesting a refresh.";

        return new AccountLinkActionRequiredException(
            connection.Id,
            connection.Provider,
            "reconnect",
            message,
            providerErrorCode);
    }

    private async Task<AccountLinkConnectionResponse> BuildConnectionResponseAsync(
        FinancialConnection connection,
        string providerDisplayName,
        CancellationToken cancellationToken)
    {
        var linkedAccounts = await _financeDbContext.PersonalLinkedAccounts
            .AsNoTracking()
            .Where(item => item.FinancialConnectionId == connection.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return MapConnectionToResponse(connection, linkedAccounts, providerDisplayName);
    }

    private static AccountLinkConnectionResponse MapConnectionToResponse(
        FinancialConnection connection,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts,
        string providerDisplayName)
    {
        return new AccountLinkConnectionResponse(
            connection.Id,
            connection.Provider,
            providerDisplayName,
            connection.ProviderConnectionReference,
            connection.InstitutionName,
            connection.InstitutionReference,
            connection.Status,
            connection.ConsentStatus,
            connection.LastSyncedAt,
            connection.LastSyncStatus,
            connection.LastError,
            connection.DisconnectedAt,
            linkedAccounts
                .Where(item => item.FinancialConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .Select(MapLinkedAccountToResponse)
                .ToList(),
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    private static AccountLinkConnectionAccountResponse MapLinkedAccountToResponse(PersonalLinkedAccount linkedAccount)
    {
        return new AccountLinkConnectionAccountResponse(
            linkedAccount.Id,
            linkedAccount.PersonalAccountId,
            linkedAccount.Name,
            linkedAccount.AccountType,
            linkedAccount.AccountSubtype,
            linkedAccount.Currency,
            linkedAccount.Last4,
            linkedAccount.Status,
            linkedAccount.LastSyncedAt,
            linkedAccount.LastSyncStatus,
            linkedAccount.LastError,
            linkedAccount.CreatedAt,
            linkedAccount.UpdatedAt);
    }

    private static AccountLinkSummaryItemResponse MapSummaryItem(
        PersonalAccount account,
        PersonalLinkedAccount? linkedAccount,
        FinancialConnection? connection)
    {
        return new AccountLinkSummaryItemResponse(
            account.Id,
            connection?.Id,
            linkedAccount?.Id,
            linkedAccount == null ? "manual" : "linked",
            account.Name,
            account.AccountType,
            account.Currency,
            account.InstitutionName,
            account.AccountSubtype,
            account.Last4,
            linkedAccount?.Status ?? account.Status,
            connection?.Provider,
            linkedAccount?.LastSyncedAt ?? connection?.LastSyncedAt,
            linkedAccount?.LastSyncStatus ?? connection?.LastSyncStatus,
            linkedAccount?.LastError ?? connection?.LastError,
            account.CreatedAt,
            account.UpdatedAt,
            account.CurrentBalance);
    }

    private static AccountLinkSessionResponse MapSessionToResponse(
        FinancialConnectionSession session,
        string providerDisplayName)
    {
        return new AccountLinkSessionResponse(
            session.Id,
            session.Provider,
            providerDisplayName,
            session.Mode,
            session.Status,
            session.FinancialConnectionId,
            session.SessionToken,
            session.ExpiresAt,
            session.CreatedAt,
            session.UpdatedAt);
    }

    private async Task<FinancialConnection?> LoadSessionTargetConnectionAsync(
        FinancialConnectionSession session,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (session.FinancialConnectionId == null)
        {
            return null;
        }

        var connection = await GetOwnedConnectionAsync(session.FinancialConnectionId.Value, tenantId, userId, cancellationToken);
        if (connection == null)
        {
            throw new InvalidOperationException("The requested account link connection could not be found for this session.");
        }

        return connection;
    }

    private Task<FinancialConnection?> GetOwnedConnectionAsync(
        Guid connectionId,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _financeDbContext.FinancialConnections
            .FirstOrDefaultAsync(
                item => item.Id == connectionId
                    && item.TenantId == tenantId
                    && item.UserId == userId,
                cancellationToken);
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

    private static string NormalizePersonalAccountType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "bank" => "Bank",
            "credit" => "CreditCard",
            "creditcard" => "CreditCard",
            "credit_card" => "CreditCard",
            "card" => "Card",
            _ => value.Trim()
        };
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeWebhookValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string? LimitText(string? value, int maxLength)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? NormalizeLast4(string? value)
    {
        var normalized = TrimNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.Length <= 4
            ? normalized
            : normalized[^4..];
    }

    private const string LinkedAccountSyncSourceType = "linked_account_sync";
}
