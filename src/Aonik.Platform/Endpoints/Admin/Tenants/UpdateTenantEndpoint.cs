using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class UpdateTenantEndpoint : Endpoint<UpdateTenantRequest, TenantResponse>
{
    private readonly ITenantService _tenantService;

    public UpdateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Patch("/admin/tenants/{tenantId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update tenant details";
            s.Description = "Partially updates the configuration and details of an existing tenant.";
            s.Response(200, "Updated tenant");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(UpdateTenantRequest req, CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _tenantService.UpdateTenantAsync(tenantId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
