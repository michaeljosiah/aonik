using Aonik.Application.Abstractions.Multitenancy;
using Microsoft.AspNetCore.Http;

namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// Provides tenant context from HTTP request headers or JWT claims.
/// </summary>
public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TenantIdClaimType = "tenant_id";
    private const string TenantIdHeaderName = "X-Tenant-Id";

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentTenantId()
    {
        if (TryGetCurrentTenantId(out var tenantId))
        {
            return tenantId;
        }

        throw new InvalidOperationException(
            "Tenant context not found. Ensure the request contains a valid tenant identifier " +
            $"in either the '{TenantIdClaimType}' claim or '{TenantIdHeaderName}' header.");
    }

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return false;
        }

        // Try to get tenant ID from JWT claims first
        var tenantIdClaim = httpContext.User?.Claims
            .FirstOrDefault(c => c.Type == TenantIdClaimType);

        if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out tenantId))
        {
            return true;
        }

        // Fall back to header
        if (httpContext.Request.Headers.TryGetValue(TenantIdHeaderName, out var headerValue))
        {
            var headerValueString = headerValue.ToString();
            if (!string.IsNullOrEmpty(headerValueString) && Guid.TryParse(headerValueString, out tenantId))
            {
                return true;
            }
        }

        return false;
    }
}
