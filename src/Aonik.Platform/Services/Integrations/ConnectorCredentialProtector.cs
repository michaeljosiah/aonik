using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;

using Aonik.Platform.Entities.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorCredentialProtector(IDataProtectionProvider protection)
{
    public string Protect(ConnectorConnection connection, ConnectorOAuthTokens tokens)
        => Credentials(connection).Protect(ConnectorJson.Serialize(tokens));
    public ConnectorOAuthTokens Read(ConnectorConnection connection)
    {
        try { return ConnectorJson.Read<ConnectorOAuthTokens>(Credentials(connection).Unprotect(connection.ProtectedCredentials)); }
        catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or ArgumentException)
        { throw ConnectorAuthorization.Access("ReauthorizationRequired"); }
    }
    public string ProtectRequest(Guid tenantId, Guid requestId, Guid userId, string purpose, string value)
        => Request(tenantId, requestId, userId, purpose).Protect(value);
    public string ReadRequest(Guid tenantId, Guid requestId, Guid userId, string purpose, string value)
        => Request(tenantId, requestId, userId, purpose).Unprotect(value);
    private IDataProtector Credentials(ConnectorConnection connection) => protection.CreateProtector("Aonik.ConnectorCredentials.v1",
        connection.TenantId.ToString("D"), connection.Id.ToString("D"), connection.ConnectorKey, connection.ProviderAccountId,
        connection.CredentialVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private IDataProtector Request(Guid tenantId, Guid requestId, Guid userId, string purpose) => protection.CreateProtector("Aonik.ConnectorConsent.v1",
        tenantId.ToString("D"), requestId.ToString("D"), userId.ToString("D"), purpose);
}
