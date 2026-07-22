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
    /// <summary>Base64url of 32 random bytes — the length every minted token has, and the floor
    /// below which a stored token is treated as legacy/weak and fails closed.</summary>
    public const int MintedTokenLength = 43;

    public static string MintToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
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
        if (stored is null || stored.Length < MintedTokenLength)
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
