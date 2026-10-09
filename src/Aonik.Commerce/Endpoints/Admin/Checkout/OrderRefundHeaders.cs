using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

internal static class OrderRefundHeaders
{
    public static void Apply(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
