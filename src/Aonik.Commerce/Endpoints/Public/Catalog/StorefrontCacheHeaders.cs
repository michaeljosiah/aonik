using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>
/// Tenant-partitioned cache semantics for the anonymous storefront surfaces (Spec 070 §9 / A14):
/// these documents are tenant-specific on tenant-less paths, so under header routing any shared
/// cache MUST key on the tenant header — without <c>Vary: X-Tenant-Id</c> it would serve tenant
/// A's labels, pricing or box plan to tenant B. Subdomain-routed deployments partition by host
/// anyway; the header is harmless there.
/// </summary>
internal static class StorefrontCacheHeaders
{
    private const string TenantHeader = "X-Tenant-Id";

    public static void Apply(HttpContext context)
    {
        var response = context.Response;
        var existing = response.Headers.Vary;

        if (!existing.Contains(TenantHeader))
        {
            response.Headers.Vary = StringValuesConcat(existing, TenantHeader);
        }
    }

    private static Microsoft.Extensions.Primitives.StringValues StringValuesConcat(
        Microsoft.Extensions.Primitives.StringValues existing, string value)
        => existing.Count == 0 ? value : Microsoft.Extensions.Primitives.StringValues.Concat(existing, value);
}
