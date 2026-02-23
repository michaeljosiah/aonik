using Aonik.Api.Contracts.Seeding;
using Aonik.Platform.Contracts.Services.Seeding;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class SeedTenantDemoEndpoint : Endpoint<DemoSeedRequest, DemoSeedResponse>
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

    public override async Task HandleAsync(DemoSeedRequest req, CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _demoSeedService.SeedAsync(tenantId, req.SeedType, ct);

        var response = new DemoSeedResponse(
            result.TenantId,
            result.SeedType,
            result.SeededAt,
            result.Operations);

        await Send.OkAsync(response, ct);
    }
}
