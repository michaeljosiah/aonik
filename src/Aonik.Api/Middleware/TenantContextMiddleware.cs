using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Infrastructure.Authentication.Configuration;

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
        ITenantResolver tenantResolver,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<TenantContextMiddleware> logger)
    {
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!tenantContext.IsResolved)
        {
            var tenantId = await tenantResolver.ResolveTenantIdAsync(context.RequestAborted);

            if (tenantId == null)
            {
                logger.LogWarning("Tenant context missing for path {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant context missing" });
                return;
            }

            tenantContext.TenantId = tenantId.Value;
            var mode = configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");
            tenantContext.ResolutionSource = mode.ToString();
        }

        await _next(context);
    }

    private static bool IsExemptPath(PathString path)
    {
        return path.StartsWithSegments("/health")
               || path.StartsWithSegments("/swagger")
               || path.StartsWithSegments("/admin")
               || path.StartsWithSegments("/bootstrap");
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
