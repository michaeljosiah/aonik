using System.Security.Cryptography;
using System.Text;

using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// The one authorizer behind every cart operation (Spec 068 R10). Tokens are server-minted —
/// 256 bits of entropy, opaque, disclosed exactly once in the create response; any client-supplied
/// token value on create is ignored. Legacy carts whose stored token is null or shorter than a
/// minted one fail closed: their token came from the old store-verbatim contract and may be empty
/// or guessable. Comparison is constant-time. Callers translate "not authorized" into the same
/// 404 an unknown cart id gets — no oracle.
/// </summary>
internal static class CartAccess
{
    /// <summary>Minted tokens are version-prefixed: length alone cannot prove provenance,
    /// because the pre-mint contract stored arbitrary client-supplied strings — a predictable
    /// 43-character legacy value must not pass as server entropy (L5).</summary>
    public const string MintedTokenPrefix = "ct1_";

    /// <summary>Prefix + base64url of 32 random bytes.</summary>
    public const int MintedTokenLength = 47;

    public static string MintToken()
        => MintedTokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    public static bool IsAuthorized(Entities.Cart.Cart cart, CartAccessContext? access)
    {
        // Party-bound cart: an authenticated principal matching BuyerPartyId — the guest token
        // is irrelevant here by design.
        if (cart.BuyerPartyId is { } party)
        {
            return access?.AuthenticatedPartyId == party;
        }

        var stored = cart.AnonymousToken;
        if (stored is null || stored.Length < MintedTokenLength
            || !stored.StartsWith(MintedTokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var presented = access?.GuestToken;
        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        var storedBytes = Encoding.UTF8.GetBytes(stored);
        var presentedBytes = Encoding.UTF8.GetBytes(presented);

        // FixedTimeEquals requires equal lengths; a length mismatch is an immediate no. Length is
        // not a secret — every minted token has the same one.
        return storedBytes.Length == presentedBytes.Length
            && CryptographicOperations.FixedTimeEquals(storedBytes, presentedBytes);
    }
}
