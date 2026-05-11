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
    /// Lists active tenants the currently-authenticated external identity
    /// (issuer + subject) has a membership in. Bypasses tenant query filters
    /// because a single identity can have User rows across multiple tenants
    /// and the caller has not yet picked one.
    /// </summary>
    /// <param name="externalIssuer">JWT <c>iss</c> claim of the caller.</param>
    /// <param name="externalSubject">JWT <c>sub</c> (or Entra <c>oid</c>) claim of the caller.</param>
    Task<MyTenantsResponse> ListTenantsForCurrentUserAsync(
        string externalIssuer,
        string externalSubject,
        CancellationToken cancellationToken = default);
}
