using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity.Provisioning;

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
