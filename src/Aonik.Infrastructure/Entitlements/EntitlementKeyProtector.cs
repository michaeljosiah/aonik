using Aonik.SharedKernel.Abstractions.Entitlements;

using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Infrastructure.Entitlements;

/// <summary>
/// Protects entitlement signing keys through the same DataProtection root as settings (Spec 090 §6):
/// the private key never leaves the server, and at rest it is ciphertext.
/// </summary>
internal sealed class EntitlementKeyProtector : IEntitlementKeyProtector
{
    private readonly IDataProtector _protector;

    public EntitlementKeyProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Aonik.Entitlements.SigningKeys");

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string value) => _protector.Unprotect(value);
}
