using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>A read-only capability for one tenant's order, using the platform's persistent key ring.</summary>
internal sealed class GuestOrderAccess(IDataProtectionProvider provider)
{
    private const string Purpose = "Aonik.Commerce.GuestOrderRead.v1";

    public string Issue(Guid tenantId, Guid orderId)
        => Protector(tenantId, orderId).Protect("read");

    public bool IsValid(string? token, Guid tenantId, Guid orderId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024
            || token.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            return false;

        try
        {
            return Protector(tenantId, orderId).Unprotect(token) == "read";
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private IDataProtector Protector(Guid tenantId, Guid orderId)
        => provider.CreateProtector(Purpose, tenantId.ToString("N"), orderId.ToString("N"));
}
