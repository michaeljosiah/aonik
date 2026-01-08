using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity.Provisioning;

public interface ITenantProvisioner
{
    Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantHealthResult> CheckTenantHealthAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
