using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class ActivateTenantEndpoint : EndpointWithoutRequest
{
    private readonly ITenantService _tenantService;

    public ActivateTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/activate");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Activate a tenant";
            s.Description = "Sets the specified tenant to an active state, enabling access and operations for its users.";
            s.Response(200, "Tenant activated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        await _tenantService.ActivateTenantAsync(tenantId, ct);
        await Send.NoContentAsync(ct);
    }
}
