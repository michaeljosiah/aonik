using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.Platform.Contracts.Services.Seeding;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class ReverseTenantDemoEndpoint : EndpointWithoutRequest<DemoSeedResponse>
{
    private readonly IDemoSeedService _demoSeedService;

    public ReverseTenantDemoEndpoint(IDemoSeedService demoSeedService)
    {
        _demoSeedService = demoSeedService;
    }

    public override void Configure()
    {
        Delete("/admin/tenants/{tenantId}/demo-seed");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Reverse tenant demo data";
            s.Description = "Removes demo data previously seeded for the specified tenant without clearing the full tenant database.";
            s.Response(200, "Reversal result");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _demoSeedService.ReverseAsync(tenantId, ct);

        var response = new DemoSeedResponse(
            result.TenantId,
            result.SeedType,
            result.SeededAt,
            result.Operations);

        await Send.OkAsync(response, ct);
    }
}
