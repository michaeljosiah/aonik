using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class GetTenantHealthEndpoint : EndpointWithoutRequest<TenantHealthResult>
{
    private readonly ITenantProvisioner _provisioner;

    public GetTenantHealthEndpoint(ITenantProvisioner provisioner)
    {
        _provisioner = provisioner;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}/health");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Check tenant health";
            s.Description = "Runs a health check on the specified tenant's provisioned resources and returns the result.";
            s.Response(200, "Health check result");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _provisioner.CheckTenantHealthAsync(tenantId, ct);
        await Send.OkAsync(result, ct);
    }
}
