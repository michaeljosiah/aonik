using Aonik.Application.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// Provides tenant context from the scoped ITenantContext.
/// </summary>
public class HttpContextTenantProvider : ITenantProvider
{
    private readonly ITenantContext _tenantContext;

    public HttpContextTenantProvider(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Guid GetCurrentTenantId()
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context not available");
        }

        return _tenantContext.TenantId.Value;
    }

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        if (_tenantContext.IsResolved && _tenantContext.TenantId.HasValue)
        {
            tenantId = _tenantContext.TenantId.Value;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
