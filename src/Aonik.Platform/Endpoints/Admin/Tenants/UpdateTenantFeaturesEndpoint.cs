using Aonik.Platform.Contracts.Api.Features;
using Aonik.Platform.Contracts.Models.Features;
using Aonik.Platform.Contracts.Services.Features;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class UpdateTenantFeaturesEndpoint : Endpoint<TenantFeatureUpdateRequest, TenantFeatureListResponse>
{
    private readonly ITenantFeatureService _tenantFeatureService;

    public UpdateTenantFeaturesEndpoint(ITenantFeatureService tenantFeatureService)
    {
        _tenantFeatureService = tenantFeatureService;
    }

    public override void Configure()
    {
        Put("/admin/tenants/{tenantId}/features");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(TenantFeatureUpdateRequest req, CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var toggles = req.Features?.Select(feature => new TenantFeatureToggle(
            feature.FeatureName,
            feature.IsEnabled,
            feature.Reason)).ToList() ?? new List<TenantFeatureToggle>();

        var result = await _tenantFeatureService.UpsertTenantFeaturesAsync(tenantId, toggles, ct);

        var response = new TenantFeatureListResponse(
            result.TenantId,
            result.Features.Select(feature => new TenantFeatureItemResponse(
                feature.FeatureName,
                feature.IsEnabled,
                feature.UpdatedAt)).ToList());

        await Send.OkAsync(response, ct);
    }
}
