using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class DeactivateTenantEndpoint : EndpointWithoutRequest
{
    private readonly ITenantService _tenantService;

    public DeactivateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/deactivate");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        await _tenantService.DeactivateTenantAsync(tenantId, ct);
        await Send.NoContentAsync(ct);
    }
}
