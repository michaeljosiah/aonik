using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;

namespace Aonik.Api.Middleware;

public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAonikDbContext dbContext,
        ITenantContext tenantContext)
    {
        // Skip health and swagger (public endpoints)
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Skip admin endpoints (they use PlatformAdmin policy, not tenant-scoped)
        if (context.Request.Path.StartsWithSegments("/admin") ||
            context.Request.Path.StartsWithSegments("/bootstrap"))
        {
            await _next(context);
            return;
        }

        // Tenant should already be resolved by TenantContextMiddleware
        if (!tenantContext.IsResolved || tenantContext.TenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context missing" });
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
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" });
            return;
        }

        if (tenant.Status != "Active")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Tenant is {tenant.Status}",
                tenantId = tenant.Id,
                status = tenant.Status
            });

            return;
        }

        await _next(context);
    }
}

public static class TenantValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantValidationMiddleware>();
    }
}
