using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Accounts;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.Accounts.Linking;

/// <summary>
/// Drives the OAuth-style handshake with an account-link provider:
/// creates a launch session, then exchanges the user's temporary code
/// for a durable connection. The connection upsert and post-exchange
/// initial transaction sync are delegated to collaborators.
/// </summary>
internal sealed class AccountLinkSessionCoordinator
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly AccountLinkProviderResolver _providerResolver;
    private readonly AccountConnectionSyncApplicator _syncApplicator;
    private readonly AccountTransactionSyncOrchestrator _transactionSyncOrchestrator;
    private readonly ILogger _logger;

    public AccountLinkSessionCoordinator(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        AccountLinkProviderResolver providerResolver,
        AccountConnectionSyncApplicator syncApplicator,
        AccountTransactionSyncOrchestrator transactionSyncOrchestrator,
        ILogger logger)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _providerResolver = providerResolver;
        _syncApplicator = syncApplicator;
        _transactionSyncOrchestrator = transactionSyncOrchestrator;
        _logger = logger;
    }

    public async Task<AccountLinkSessionResponse> CreateSessionAsync(
        CreateAccountLinkSessionRequest request,
        CancellationToken cancellationToken)
    {
        AccountLinkingNormalization.ValidateRequiredText(request.Provider, nameof(request.Provider));

        var gateway = _providerResolver.Resolve(request.Provider);
        var mode = AccountLinkingNormalization.NormalizeMode(request.Mode);
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
                AccountLinkingNormalization.TrimNullable(request.CountryCode),
                AccountLinkingNormalization.TrimNullable(request.ClientName),
                null), // PhoneNumber
            cancellationToken);

        session.SessionToken = providerSession.LaunchToken;
        session.ProviderSessionReference = providerSession.ProviderSessionReference;
        session.ExpiresAt = providerSession.ExpiresAt;

        _financeDbContext.AccountConnectionSessions.Add(session);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return AccountConnectionResponseMapper.MapSession(session, gateway.DisplayName);
    }

    public async Task<AccountLinkExchangeResponse> ExchangeSessionAsync(
        ExchangeAccountLinkSessionRequest request,
        CancellationToken cancellationToken)
    {
        AccountLinkingNormalization.ValidateRequiredText(request.TemporaryCode, nameof(request.TemporaryCode));

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

        var gateway = _providerResolver.Resolve(session.Provider);
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

        var connection = await _syncApplicator.ApplyProviderSyncAsync(
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

        var response = await _syncApplicator.BuildConnectionResponseAsync(
            connection,
            gateway.DisplayName,
            cancellationToken);

        return new AccountLinkExchangeResponse(session.Id, response);
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

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
