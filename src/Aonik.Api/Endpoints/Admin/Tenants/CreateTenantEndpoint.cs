using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class CreateTenantEndpoint : Endpoint<CreateTenantRequest, TenantResponse>
{
    private readonly ITenantService _tenantService;

    public CreateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants");
        Policies("Tenants.Write");
    }

    public override async Task HandleAsync(CreateTenantRequest req, CancellationToken ct)
    {
        var result = await _tenantService.CreateTenantAsync(req, ct);

        await Send.CreatedAtAsync<GetTenantEndpoint>(
            routeValues: new { tenantId = result.Id },
            responseBody: result,
            cancellation: ct);
    }
}
