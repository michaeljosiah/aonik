using Aonik.Platform.Contracts.Api.Features;
using Aonik.Platform.Contracts.Services.Features;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class GetTenantFeaturesEndpoint : EndpointWithoutRequest<TenantFeatureListResponse>
{
    private readonly ITenantFeatureService _tenantFeatureService;

    public GetTenantFeaturesEndpoint(ITenantFeatureService tenantFeatureService)
    {
        _tenantFeatureService = tenantFeatureService;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}/features");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant feature flags";
            s.Description = "Retrieves all feature flags and their enabled/disabled state for the specified tenant.";
            s.Response(200, "Feature flag list");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _tenantFeatureService.GetTenantFeaturesAsync(tenantId, ct);

        var response = new TenantFeatureListResponse(
            result.TenantId,
            result.Features.Select(feature => new TenantFeatureItemResponse(
                feature.FeatureName,
                feature.IsEnabled,
                feature.UpdatedAt)).ToList());

        await Send.OkAsync(response, ct);
    }
}
