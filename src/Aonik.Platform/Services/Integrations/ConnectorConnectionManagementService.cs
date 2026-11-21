using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorConnectionManagementService(PlatformDbContext database, ConnectorAuthorization authorization,
    ConnectorGrantService grants, ConnectorCredentialProtector protector, IEnumerable<IConnectorOAuthProvider> providers,
    IAuditLogWriter audit, IClock clock)
{
    public async Task<IReadOnlyList<ConnectorConnectionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        bool manager = await authorization.CanManageAsync(userId, cancellationToken);
        return (await database.ConnectorConnections.AsNoTracking().Where(connection => connection.TenantId == authorization.TenantId
            && (connection.OwnerUserId == userId || (manager && connection.Ownership == "Tenant")))
            .OrderBy(connection => connection.DisplayName).Take(100).ToListAsync(cancellationToken)).Select(ConnectorService.Summary).ToList();
    }

    public async Task<ConnectorConnectionSummary> RenameAsync(Guid connectionId, string displayName, string expectedVersion, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnection connection = await FindAsync(connectionId, cancellationToken);
        await grants.RequireManagerAsync(connection, userId, cancellationToken);
        ConnectorGrantService.Version(connection.RowVersion, expectedVersion);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 160 || displayName.Any(char.IsControl)) throw new ArgumentException("Provide a label of 1–160 characters.");
        await audit.LogAsync("Connector.AccountRenamed", "ConnectorConnection", connection.Id, connection.TenantId, userId, null, null, cancellationToken);
        connection.DisplayName = displayName.Trim();
        await SaveAsync(cancellationToken);
        return ConnectorService.Summary(connection);
    }

    public async Task<ConnectorConnectionSummary> DisconnectAsync(Guid connectionId, string expectedVersion, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnection connection = await FindAsync(connectionId, cancellationToken);
        await grants.RequireManagerAsync(connection, userId, cancellationToken);
        ConnectorGrantService.Version(connection.RowVersion, expectedVersion);
        string? token = null;
        try { if (connection.ProtectedCredentials.Length > 0) token = protector.Read(connection).RefreshToken; }
        catch (ConnectorAccessException) { /* Local revocation must still work after key loss. */ }
        await audit.LogAsync("Connector.AccountDisconnectionRequested", "ConnectorConnection", connection.Id, connection.TenantId, userId, null, null, cancellationToken);
        connection.Status = "Revoked"; connection.ProtectedCredentials = ""; connection.CredentialVersion++; connection.TokenExpiresAt = null;
        connection.RefreshLeaseId = null; connection.RefreshLeaseExpiresAt = null;
        foreach (ConnectorConnectionGrant grant in await database.ConnectorConnectionGrants.Where(grant => grant.TenantId == connection.TenantId
            && grant.ConnectionId == connection.Id && grant.RevokedAt == null).ToListAsync(cancellationToken))
        { grant.RevokedAt = clock.UtcNow; grant.RevokedByUserId = userId; }
        foreach (ConnectorConnectionRequest request in await database.ConnectorConnectionRequests.Where(request => request.TenantId == connection.TenantId
            && request.ConnectionId == connection.Id && (request.Status == "Pending" || request.Status == "AwaitingConsent")).ToListAsync(cancellationToken))
        {
            request.Status = "Cancelled"; request.ActiveRequestHash = null; request.ReasonCode = "ConnectionRevoked";
            request.NextAction = "The account owner disconnected this connection.";
            ConnectorConnectionRequestService.ClearConsent(request);
        }
        await SaveAsync(cancellationToken);
        if (token is not null && providers.SingleOrDefault(provider => provider.ConnectorKey == connection.ConnectorKey && provider.IsConfigured) is { } provider)
        {
            try { await provider.RevokeAsync(token, cancellationToken); }
            catch (Exception error) when (error is ConnectorProviderException or HttpRequestException or OperationCanceledException)
            { await audit.LogAsync("Connector.ProviderRevocationUncertain", "ConnectorConnection", connection.Id, connection.TenantId, userId, null, null, CancellationToken.None); }
        }
        return ConnectorService.Summary(connection);
    }
    private async Task<ConnectorConnection> FindAsync(Guid id, CancellationToken cancellationToken) => await database.ConnectorConnections
        .SingleOrDefaultAsync(connection => connection.TenantId == authorization.TenantId && connection.Id == id, cancellationToken) ?? throw new KeyNotFoundException("Connection not found.");
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConnectorAccessException("Conflict", "The account changed. Reload before applying another change."); }
    }
}
