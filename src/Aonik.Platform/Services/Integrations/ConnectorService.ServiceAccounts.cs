using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed partial class ConnectorService
{
    public async Task<ConnectorConnectionSummary> ProvisionServiceAccountAsync(string connectorKey, string providerAccountId, string displayName,
        CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Manage", cancellationToken);
        await authorization.RequireActorAsync(new("User", userId, userId), "Workflow.Write", cancellationToken);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 160 || displayName.Any(char.IsControl))
            throw new ArgumentException("Provide a label of 1–160 characters.");
        IConnectorServiceAccountProvider provider = authorization.ServiceProvider(connectorKey) ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
        ConnectorPolicy policy = await policies.GetAsync(connectorKey, cancellationToken);
        if (policy.Code != "Ready") throw ConnectorAuthorization.Access(policy.Code);
        ConnectorServiceAccount account = await provider.RequireAccountAsync(providerAccountId, cancellationToken: cancellationToken);
        if (account.ConnectorKey != connectorKey || account.ProviderAccountId != providerAccountId || !account.Actions.Any(item => policy.AllowedActions.Contains(item.Action)))
            throw ConnectorAuthorization.Access("ForbiddenByPolicy");
        Guid tenantId = authorization.TenantId;
        IQueryable<ConnectorConnection> existing = database.ConnectorConnections.AsNoTracking().Where(connection => connection.TenantId == tenantId
            && connection.ConnectorKey == connectorKey && connection.Ownership == "Tenant" && connection.ProviderAccountId == providerAccountId && connection.Status != "Revoked");
        ConnectorConnection? replay = await existing.SingleOrDefaultAsync(cancellationToken);
        if (replay is not null) return Summary(replay);
        var connection = new ConnectorConnection
        {
            TenantId = tenantId, ConnectorKey = connectorKey, Ownership = "Tenant", ProviderAccountId = account.ProviderAccountId,
            DisplayName = displayName.Trim(), ProviderAccountLabel = account.DisplayName, ProvisionedByUserId = userId,
            Status = "Active", CredentialVersion = 1, LastHealthyAt = clock.UtcNow
        };
        await audit.LogAsync("Connector.ServiceAccountProvisioningRequested", "ConnectorConnection", connection.Id, tenantId, userId, null,
            ConnectorJson.Serialize(new { connectorKey, account.ProviderAccountId, account.Actions }), cancellationToken);
        database.ConnectorConnections.Add(connection);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            database.Entry(connection).State = EntityState.Detached;
            return Summary(await existing.SingleAsync(cancellationToken));
        }
        return Summary(connection);
    }
}
