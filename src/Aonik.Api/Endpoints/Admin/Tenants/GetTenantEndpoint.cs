using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class GetTenantEndpoint : EndpointWithoutRequest<TenantResponse>
{
    private readonly ITenantService _tenantService;

    public GetTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        
        var result = await _tenantService.GetTenantAsync(tenantId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
