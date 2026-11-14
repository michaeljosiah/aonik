using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Integrations;

/// <summary>Rowversion/lease-fenced refresh of one account. No host token or other account fallback exists.</summary>
internal sealed class ConnectorCredentialService(PlatformDbContext database, ITenantProvider tenantProvider,
    ConnectorCredentialProtector protector, IEnumerable<IConnectorOAuthProvider> providers, IClock clock)
{
    public async Task<string> AccessTokenAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        ConnectorConnection connection = await database.ConnectorConnections.SingleAsync(connection => connection.TenantId == tenantId
            && connection.Id == connectionId, cancellationToken);
        await database.Entry(connection).ReloadAsync(cancellationToken);
        if (connection.Status != "Active") throw ConnectorAuthorization.Access("ReauthorizationRequired");
        ConnectorOAuthTokens current;
        try { current = protector.Read(connection); }
        catch (ConnectorAccessException)
        {
            await MarkReauthorizationAsync(connectionId, cancellationToken);
            throw;
        }
        if (current.ExpiresAt > clock.UtcNow.AddSeconds(45)) return current.AccessToken;
        if (string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            await MarkReauthorizationAsync(connectionId, cancellationToken);
            throw ConnectorAuthorization.Access("ReauthorizationRequired");
        }
        IConnectorOAuthProvider provider = providers.SingleOrDefault(provider => provider.ConnectorKey == connection.ConnectorKey)
            ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
        if (!provider.IsConfigured) throw ConnectorAuthorization.Access("ConnectorUnavailable");
        if (connection.RefreshLeaseExpiresAt > clock.UtcNow) throw new ConnectorProviderException("CredentialRefreshBusy", true);
        Guid lease = Guid.NewGuid();
        int version = connection.CredentialVersion;
        connection.RefreshLeaseId = lease;
        connection.RefreshLeaseExpiresAt = clock.UtcNow.AddMinutes(2);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConnectorProviderException("CredentialRefreshBusy", true); }
        try
        {
            ConnectorOAuthTokens refreshed = await provider.RefreshAsync(current.RefreshToken, current.GrantedScopes ?? [], cancellationToken);
            ConnectorAccountIdentity identity = await provider.GetIdentityAsync(refreshed.AccessToken, cancellationToken);
            if (identity.Id != connection.ProviderAccountId) throw new ConnectorProviderException("ProviderAccountChanged");
            await database.Entry(connection).ReloadAsync(cancellationToken);
            if (connection.RefreshLeaseId != lease || connection.CredentialVersion != version || connection.Status != "Active")
                throw new ConnectorProviderException("CredentialRefreshSuperseded", true);
            IReadOnlyList<string> scopes = refreshed.GrantedScopes ?? current.GrantedScopes ?? [];
            var updated = refreshed with { RefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken) ? current.RefreshToken : refreshed.RefreshToken,
                GrantedScopes = scopes };
            connection.CredentialVersion++;
            connection.GrantedScopesJson = ConnectorJson.Serialize(scopes);
            connection.TokenExpiresAt = updated.ExpiresAt;
            connection.ProviderAccountLabel = identity.Label;
            connection.ProtectedCredentials = protector.Protect(connection, updated);
            connection.LastHealthyAt = clock.UtcNow;
            connection.LastErrorCode = null;
            await database.SaveChangesAsync(cancellationToken);
            return updated.AccessToken;
        }
        catch (ConnectorProviderException exception) when (exception.Code is "ReauthorizationRequired" or "ProviderAccountChanged")
        {
            await MarkReauthorizationAsync(connectionId, CancellationToken.None);
            throw ConnectorAuthorization.Access("ReauthorizationRequired");
        }
        finally
        {
            await database.Entry(connection).ReloadAsync(CancellationToken.None);
            if (connection.RefreshLeaseId == lease)
            {
                connection.RefreshLeaseId = null;
                connection.RefreshLeaseExpiresAt = null;
                try { await database.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { }
            }
        }
    }

    public async Task MarkReauthorizationAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        ConnectorConnection? connection = await database.ConnectorConnections.SingleOrDefaultAsync(connection => connection.TenantId == tenantProvider.GetCurrentTenantId()
            && connection.Id == connectionId, cancellationToken);
        if (connection is null) return;
        await database.Entry(connection).ReloadAsync(cancellationToken);
        if (connection.Status == "Revoked") return;
        connection.Status = "ReauthorizationRequired";
        connection.LastErrorCode = "ReauthorizationRequired";
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { }
    }
}
