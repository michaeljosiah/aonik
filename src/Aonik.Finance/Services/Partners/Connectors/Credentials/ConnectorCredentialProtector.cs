using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Finance.Services.Partners.Connectors.Credentials;

/// <summary>
/// Encrypts/decrypts partner connector credential bundles (Spec 042 §6). Backed by the same ASP.NET Data
/// Protection substrate Platform's <c>ISettingValueProtector</c> and Agents' <c>ITenantCredentialProtector</c>
/// use (keys persisted via <c>IDataProtectionKeyContext</c> on <c>AonikDbContext</c>), with a distinct purpose
/// string so a key-ring rotation isolates these secrets. Bundle secrets are encrypted at rest in
/// <c>CredentialBundle.ProtectedSecretsJson</c>, decrypted only server-side at connector-build time, and never
/// returned to any client.
/// </summary>
public interface IConnectorCredentialProtector
{
    /// <summary>Encrypt a plaintext secret blob for storage.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypt a stored secret blob for use at connector-build time.</summary>
    string Unprotect(string protectedValue);
}

/// <summary>Default <see cref="IConnectorCredentialProtector"/> over <see cref="IDataProtectionProvider"/>.</summary>
internal sealed class ConnectorCredentialProtector : IConnectorCredentialProtector
{
    private readonly IDataProtector _protector;

    public ConnectorCredentialProtector(IDataProtectionProvider provider)
    {
        // Purpose string isolates connector credentials from other Data Protection consumers.
        _protector = provider.CreateProtector("Aonik.Finance.PartnerConnectorCredentials.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
