using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Deactivate a tenant";
            s.Description = "Sets the specified tenant to an inactive state, disabling access and operations for its users.";
            s.Response(200, "Tenant deactivated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        await _tenantService.DeactivateTenantAsync(tenantId, ct);
        await Send.NoContentAsync(ct);
    }
}
