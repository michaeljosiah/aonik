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
            // Skip health, swagger, scalar, and the anonymous public-settings discovery
            // endpoints (GET /v1/settings/auth-provider and /v1/settings/public). The auth
            // provider is configured per-deployment (ADR-007), not per-tenant, so these
            // settings are global and safe to serve without tenant context. Without this
            // skip those AllowAnonymous endpoints are unreachable — they 401 "Tenant
            // context missing" for any caller (e.g. the CLI) with no resolvable tenant.
            // Only the two anonymous paths are skipped; the authenticated, tenant-scoped
            // /v1/settings/user and /v1/settings/resolved endpoints still get validated.
            if (context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/alive") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/scalar") ||
                context.Request.Path.StartsWithSegments("/v1/settings/auth-provider") ||
                context.Request.Path.StartsWithSegments("/v1/settings/public") ||
                HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Skip admin endpoints (they use platform/global policies, not tenant-scoped validation)
            if (context.Request.Path.StartsWithSegments("/host") ||
                context.Request.Path.StartsWithSegments("/admin") ||
                context.Request.Path.StartsWithSegments("/integrations") ||
                context.Request.Path.StartsWithSegments("/bootstrap"))
            {
                await _next(context);
                return;
            }

            // Skip the CodeAct callback endpoint — the Python sandbox calling
            // back in has no JWT (auth is the nonce in the URL path), so there
            // is no tenant to resolve at this point. The endpoint itself
            // re-establishes tenant + user scope from the signed nonce payload
            // before dispatching the named host tool.
            // See src/Aonik.Finance/Endpoints/Agents/CodeActCallbackEndpoint.cs
            // and docs/runbooks/codeact-sandbox-providers.md.
            if (context.Request.Path.StartsWithSegments("/ai/codeact/call-tool"))
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
