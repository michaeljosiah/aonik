using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// A simple tenant provider that uses a fixed tenant ID.
/// Useful for testing, background jobs, or single-tenant scenarios.
/// </summary>
public class StaticTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;

    public StaticTenantProvider(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    public Guid GetCurrentTenantId()
    {
        if (_tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant ID has not been set.");
        }

        return _tenantId;
    }

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return _tenantId != Guid.Empty;
    }
}
