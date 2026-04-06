using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class CreateTenantEndpoint : Endpoint<CreateTenantRequest, TenantResponse>
{
    private readonly ITenantService _tenantService;

    public CreateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create a new tenant";
            s.Description = "Creates a new tenant record with the provided configuration. Returns the created tenant details.";
            s.Response(201, "Tenant created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tenant Administration"));
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
