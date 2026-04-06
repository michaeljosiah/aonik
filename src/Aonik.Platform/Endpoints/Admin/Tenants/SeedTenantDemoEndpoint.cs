using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.Platform.Contracts.Services.Seeding;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class SeedTenantDemoEndpoint : Endpoint<DemoSeedRequest, DemoSeedResponse>
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
        Summary(s =>
        {
            s.Summary = "Seed tenant with demo data";
            s.Description = "Populates the specified tenant with sample demo data of the requested seed type for testing purposes.";
            s.Response(200, "Seed result");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
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
