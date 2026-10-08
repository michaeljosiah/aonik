using Aonik.Commerce.Contracts.Models.Checkout;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

/// <summary>
/// Translates the transport into a <see cref="CartAccessContext"/> (Spec 068 R10): the guest
/// token travels in the <c>X-Cart-Token</c> header — never the URL, because URLs leak. The
/// authenticated party comes from the principal. The optional version header carries a write
/// precondition; services authorize first and then enforce it for new writes.
/// </summary>
internal static class CartRequestAccess
{
    public const string HeaderName = "X-Cart-Token";
    public const string VersionHeaderName = "X-Cart-Version";

    /// <summary>Both halves (Spec 072 Y2): the guest token from the header, and the
    /// authenticated principal's party from the platform resolver — null for anonymous callers,
    /// which leaves guest semantics exactly as they were.</summary>
    public static async Task<CartAccessContext> FromAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var token = httpContext.Request.Headers[HeaderName].FirstOrDefault();
        var resolver = httpContext.RequestServices.GetRequiredService<Aonik.SharedKernel.Abstractions.ICurrentPartyResolver>();
        var partyId = await resolver.GetCurrentPartyIdAsync(cancellationToken);
        var versions = httpContext.Request.Headers[VersionHeaderName];
        var version = versions.Count == 1 && versions[0] is { Length: <= 64 } value
            && !value.Any(char.IsWhiteSpace) ? value : null;
        return new CartAccessContext(
            string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
            partyId,
            version);
    }
}
