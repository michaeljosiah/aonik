using System;
using Aonik.Application.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Multitenancy;

/// <summary>
/// Default implementation of <see cref="ICurrentTenant"/> that uses <see cref="ITenantProvider"/>.
/// </summary>
public class CurrentTenant : ICurrentTenant
{
    private readonly ITenantProvider _tenantProvider;

    public CurrentTenant(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid? Id => _tenantProvider.TryGetCurrentTenantId(out var id) ? id : null;

    public string? Name => null;

    public bool IsAvailable => Id.HasValue;

    public IDisposable Change(Guid? tenantId)
    {
        return new TenantChangeScope(_tenantProvider, tenantId);
    }

    private class TenantChangeScope : IDisposable
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly Guid? _previousTenantId;

        public TenantChangeScope(ITenantProvider tenantProvider, Guid? tenantId)
        {
            _tenantProvider = tenantProvider;
            _previousTenantId = tenantId.HasValue 
                ? tenantId 
                : (_tenantProvider.TryGetCurrentTenantId(out var id) ? id : null);

            // We need to temporarily set the tenant context
            // This is a simplified implementation - in production you'd want a proper scoped context
        }

        public void Dispose()
        {
            // Restore previous tenant context if needed
        }
    }
}
