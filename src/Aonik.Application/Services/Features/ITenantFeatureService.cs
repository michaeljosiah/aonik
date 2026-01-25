using Aonik.Application.Models.Features;

namespace Aonik.Application.Services.Features;

public interface ITenantFeatureService
{
    Task<TenantFeatureList> GetTenantFeaturesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantFeatureList> UpsertTenantFeaturesAsync(Guid tenantId, IReadOnlyList<TenantFeatureToggle> toggles, CancellationToken cancellationToken = default);
}
