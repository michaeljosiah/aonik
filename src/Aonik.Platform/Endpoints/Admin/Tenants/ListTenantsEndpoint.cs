using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class ListTenantsEndpoint : Endpoint<ListTenantsRequest, PagedResult<TenantResponse>>
{
    private readonly ITenantService _tenantService;

    public ListTenantsEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Get("/admin/tenants");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListTenantsRequest req, CancellationToken ct)
    {
        var result = await _tenantService.ListTenantsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
