using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class DeactivateTenantEndpoint : EndpointWithoutRequest
{
    private readonly ITenantService _tenantService;

    public DeactivateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/deactivate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        
        await _tenantService.DeactivateTenantAsync(tenantId, ct);
        await Send.NoContentAsync(ct);
    }
}
