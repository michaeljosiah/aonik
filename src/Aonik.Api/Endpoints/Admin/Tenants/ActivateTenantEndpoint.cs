using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class ActivateTenantEndpoint : EndpointWithoutRequest
{
    private readonly ITenantService _tenantService;

    public ActivateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/activate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        
        await _tenantService.ActivateTenantAsync(tenantId, ct);
        await Send.NoContentAsync(ct);
    }
}
