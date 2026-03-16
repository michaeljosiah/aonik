using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;

namespace Aonik.Api.Middleware;

public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantValidationMiddleware> _logger;

    public TenantValidationMiddleware(RequestDelegate next, ILogger<TenantValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAonikDbContext dbContext,
        ITenantContext tenantContext)
    {
        try
        {
            // Skip health and swagger (public endpoints)
            if (context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Skip admin endpoints (they use PlatformAdmin policy, not tenant-scoped)
            if (context.Request.Path.StartsWithSegments("/host") ||
                context.Request.Path.StartsWithSegments("/bootstrap"))
            {
                await _next(context);
                return;
            }

            // Tenant should already be resolved by TenantContextMiddleware
            if (!tenantContext.IsResolved || tenantContext.TenantId is null)
            {
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

            var tenantId = tenantContext.TenantId.Value;

            // Validate tenant status (tenant existence already validated during JIT user creation)
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, context.RequestAborted);


            if (tenant == null)
            {
                // Should not happen (user creation validates tenant)
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                try
                {
                    await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" }, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                return;
            }

            if (tenant.Status != "Active")
            {
                _logger.LogWarning(
                    "Tenant {TenantId} is not active (Status: {TenantStatus}). Returning 403 for {Method} {Path}",
                    tenant.Id, tenant.Status, context.Request.Method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                try
                {
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = $"Tenant is {tenant.Status}",
                        tenantId = tenant.Id,
                        status = tenant.Status
                    }, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                return;
            }

            await _next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; ignore.
        }
    }
}

public static class TenantValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantValidationMiddleware>();
    }
}
