using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class ListTenantsEndpoint : Endpoint<ListTenantsRequest, PagedResult<TenantResponse>>
{
    private readonly ITenantService _tenantService;

    public ListTenantsEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Get("/admin/tenants");
        Policies("Tenants.Read");
    }

    public override async Task HandleAsync(ListTenantsRequest req, CancellationToken ct)
    {
        var result = await _tenantService.ListTenantsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
