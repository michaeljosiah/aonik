using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class UpdateTenantEndpoint : Endpoint<UpdateTenantRequest, TenantResponse>
{
    private readonly ITenantService _tenantService;

    public UpdateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Patch("/admin/tenants/{tenantId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateTenantRequest req, CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        
        var result = await _tenantService.UpdateTenantAsync(tenantId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
