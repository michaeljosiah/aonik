using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class ProvisionTenantEndpoint : EndpointWithoutRequest<ProvisionTenantResult>
{
    private readonly ITenantProvisioner _provisioner;

    public ProvisionTenantEndpoint(ITenantProvisioner provisioner)
    {
        _provisioner = provisioner;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/provision");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Provision a tenant";
            s.Description = "Triggers the provisioning workflow for the specified tenant, setting up required resources and configuration.";
            s.Response(200, "Provisioning result");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _provisioner.ProvisionTenantAsync(tenantId, ct);
        await Send.OkAsync(result, ct);
    }
}
