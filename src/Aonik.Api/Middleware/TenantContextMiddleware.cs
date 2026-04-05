using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
 
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.SharedKernel.Abstractions.Multitenancy;

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
        try
        {
            var path = context.Request.Path;

            if (path.StartsWithSegments("/health") ||
                path.StartsWithSegments("/alive") ||
                path.StartsWithSegments("/swagger") ||
                path.StartsWithSegments("/scalar") ||
                path.StartsWithSegments("/host") ||
                path.StartsWithSegments("/bootstrap") ||
                HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                if (!tenantContext.IsResolved)
                {
                    var resolvedTenantId = ResolveAnonymousTenantId(context, tenantResolver);
                    if (resolvedTenantId != null)
                    {
                        tenantContext.TenantId = resolvedTenantId;
                        tenantContext.ResolutionSource = configuration["Auth:TenantRouting"] ?? "Resolver";
                    }
                }

                await _next(context);
                return;
            }

            if (!tenantContext.IsResolved)
            {
                var resolvedTenantId = tenantResolver.ResolveTenantId() ?? tenantResolver.ResolveFromHttpContext();
                if (resolvedTenantId != null)
                {
                    tenantContext.TenantId = resolvedTenantId;
                    tenantContext.ResolutionSource = configuration["Auth:TenantRouting"] ?? "Resolver";
                }

                if (!tenantContext.IsResolved)
                {
                    logger.LogWarning("Tenant context not resolved after authentication for path {Path}", path);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    try
                    {
                        await context.Response.WriteAsJsonAsync(new { error = "Tenant context missing" }, context.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    return;
                }
            }

            await _next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; ignore.
        }
    }

    private static Guid? ResolveAnonymousTenantId(HttpContext context, ITenantResolver tenantResolver)
    {
        var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (Guid.TryParse(headerValue, out var tenantId))
        {
            return tenantId;
        }

        return tenantResolver.ResolveFromHttpContext();
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
