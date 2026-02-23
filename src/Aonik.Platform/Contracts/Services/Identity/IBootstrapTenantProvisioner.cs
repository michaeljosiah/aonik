using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

/// <summary>
/// Bootstrap-only tenant provisioning.
///
/// This is used during the initial setup flow where there may be no authenticated user
/// yet (no principal / no permissions). It must never be used for normal runtime admin
/// operations.
/// </summary>
public interface IBootstrapTenantProvisioner
{
    Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
