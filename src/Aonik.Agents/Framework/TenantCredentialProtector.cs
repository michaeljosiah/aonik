using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Agents.Framework;

/// <summary>
/// Encrypts/decrypts tenant MCP/HTTP auth secrets (Spec 033 §11). Backed by the same ASP.NET
/// Data Protection substrate that Platform's <c>ISettingValueProtector</c> uses (keys persisted via
/// <c>IDataProtectionKeyContext</c>), but defined inside the Agents module because the module
/// boundary only lets Agents depend on SharedKernel — not on Aonik.Platform. Secrets are encrypted
/// at rest in the row's <c>ProtectedAuthJson</c>, decrypted only server-side at connect/call time,
/// and never returned to any client.
/// </summary>
public interface ITenantCredentialProtector
{
    /// <summary>Encrypt a plaintext secret blob for storage.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypt a stored secret blob for use at connect/call time.</summary>
    string Unprotect(string protectedValue);
}

/// <summary>Default <see cref="ITenantCredentialProtector"/> over <see cref="IDataProtectionProvider"/>.</summary>
internal sealed class TenantCredentialProtector : ITenantCredentialProtector
{
    private readonly IDataProtector _protector;

    public TenantCredentialProtector(IDataProtectionProvider provider)
    {
        // Purpose string isolates these secrets from other Data Protection consumers.
        _protector = provider.CreateProtector("Aonik.Agents.TenantToolCredentials.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
