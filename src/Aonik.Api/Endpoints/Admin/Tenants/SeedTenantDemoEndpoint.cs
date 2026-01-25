using Aonik.Api.Contracts.Seeding;
using Aonik.Application.Services.Seeding;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class SeedTenantDemoEndpoint : EndpointWithoutRequest<DemoSeedResponse>
{
    private readonly IDemoSeedService _demoSeedService;

    public SeedTenantDemoEndpoint(IDemoSeedService demoSeedService)
    {
        _demoSeedService = demoSeedService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/demo-seed");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _demoSeedService.SeedAsync(tenantId, ct);

        var response = new DemoSeedResponse(
            result.TenantId,
            result.SeededAt,
            result.Operations);

        await Send.OkAsync(response, ct);
    }
}
