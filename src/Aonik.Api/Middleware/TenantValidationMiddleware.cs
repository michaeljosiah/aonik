using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Api.Middleware;

public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, IAonikDbContext dbContext)
    {
        // Skip validation for admin endpoints (tenant management)
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            await _next(context);
            return;
        }

        // Skip validation for health checks and swagger
        if (context.Request.Path.StartsWithSegments("/health") || 
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Try to get tenant ID from request
        if (!tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            // If tenant is required but not present, return 400
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant ID is required. Provide X-Tenant-Id header or tenant_id claim in JWT." });
            return;
        }

        // Validate tenant exists and is active
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = $"Tenant {tenantId} not found." });
            return;
        }

        if (tenant.Status == TenantStatus.Deactivated)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = $"Tenant {tenantId} is deactivated." });
            return;
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = $"Tenant {tenantId} is suspended." });
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
