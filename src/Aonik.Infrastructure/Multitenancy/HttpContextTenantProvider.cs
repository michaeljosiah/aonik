using Aonik.Application.Abstractions.Multitenancy;
using Microsoft.AspNetCore.Http;

namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// Provides tenant context from HttpContext.Items (populated by OnTokenValidated).
/// </summary>
public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.Items["AonikTenantId"] as Guid?;
        
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context not available");
        }
        
        return tenantId.Value;
    }

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _httpContextAccessor.HttpContext?.Items["AonikTenantId"] as Guid? ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }
}
