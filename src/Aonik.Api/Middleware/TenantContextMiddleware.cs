using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Multitenancy;

namespace Aonik.Api.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ILogger<TenantContextMiddleware> logger)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/health") ||
            path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/admin") ||
            path.StartsWithSegments("/bootstrap"))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!tenantContext.IsResolved)
        {
            logger.LogWarning("Tenant context not resolved after authentication for path {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context missing" });
            return;
        }

        await _next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
