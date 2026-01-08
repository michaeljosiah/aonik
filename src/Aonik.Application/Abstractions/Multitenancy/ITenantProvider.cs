namespace Aonik.Application.Abstractions.Multitenancy;

public interface ITenantProvider
{
    Guid GetCurrentTenantId();
    bool TryGetCurrentTenantId(out Guid tenantId);
}
