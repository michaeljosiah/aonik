using Aonik.Commerce.Contracts.Models.Checkout;

using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

/// <summary>
/// Translates the transport into a <see cref="CartAccessContext"/> (Spec 068 R10): the guest
/// token travels in the <c>X-Cart-Token</c> header — never the URL, because URLs leak. The
/// authenticated-party half stays empty until the storefront customer-identity capability maps
/// principals to parties; party-bound carts therefore authorize only through consumers that know
/// the party, and everything else fails closed to 404.
/// </summary>
internal static class CartRequestAccess
{
    public const string HeaderName = "X-Cart-Token";

    public static CartAccessContext From(HttpContext httpContext)
    {
        var token = httpContext.Request.Headers[HeaderName].FirstOrDefault();
        return CartAccessContext.ForGuest(string.IsNullOrWhiteSpace(token) ? null : token.Trim());
    }
}
