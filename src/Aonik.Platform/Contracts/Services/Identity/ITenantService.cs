using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface ITenantService
{
    Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantResponse>> ListTenantsAsync(ListTenantsRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task DeactivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active tenants for login dropdown (public, no authentication required).
    /// Returns minimal info: TenantId, Name, Subdomain, Environment.
    /// </summary>
    Task<TenantListForLoginResponse> ListTenantsForLoginAsync(CancellationToken cancellationToken = default);
}
