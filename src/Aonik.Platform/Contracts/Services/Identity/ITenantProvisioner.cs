using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface ITenantProvisioner
{
    Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantHealthResult> CheckTenantHealthAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
