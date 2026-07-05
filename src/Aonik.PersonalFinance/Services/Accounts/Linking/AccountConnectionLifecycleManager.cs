using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Lifecycle operations on already-established <see cref="AccountConnection"/>
/// rows: list, refresh (re-pull state from the provider), and disconnect.
/// </summary>
internal sealed class AccountConnectionLifecycleManager
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly AccountLinkProviderResolver _providerResolver;
    private readonly AccountConnectionSyncApplicator _syncApplicator;

    public AccountConnectionLifecycleManager(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        AccountLinkProviderResolver providerResolver,
        AccountConnectionSyncApplicator syncApplicator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _providerResolver = providerResolver;
        _syncApplicator = syncApplicator;
    }

    public async Task<IReadOnlyList<AccountConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected,
        CancellationToken cancellationToken)
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
            .Select(item => AccountConnectionResponseMapper.MapConnection(
                item,
                linkedAccounts,
                _providerResolver.Resolve(item.Provider).DisplayName))
            .ToList();
    }

    public async Task<AccountConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
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

        var gateway = _providerResolver.Resolve(connection.Provider);
        var providerRefresh = await gateway.RefreshConnectionAsync(
            new AccountLinkProviderRefreshRequest(
                tenantId,
                userId,
                connection.Id,
                connection.ProviderConnectionReference,
                connection.SecretReference),
            cancellationToken);

        await _syncApplicator.ApplyProviderSyncAsync(
            connection,
            gateway.ProviderCode,
            providerRefresh,
            tenantId,
            userId,
            utcNow,
            cancellationToken);

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return await _syncApplicator.BuildConnectionResponseAsync(connection, gateway.DisplayName, cancellationToken);
    }

    public async Task<AccountConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
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
            await _providerResolver.Resolve(connection.Provider).DisconnectConnectionAsync(
                new AccountLinkProviderDisconnectRequest(
                    tenantId,
                    userId,
                    connection.Id,
                    connection.ProviderConnectionReference,
                    connection.SecretReference),
                cancellationToken);

            AccountConnectionStateMutator.ApplyLocalDisconnect(connection, linkedAccounts, utcNow, "Disconnected");

            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }

        return AccountConnectionResponseMapper.MapConnection(
            connection,
            linkedAccounts,
            _providerResolver.Resolve(connection.Provider).DisplayName);
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
