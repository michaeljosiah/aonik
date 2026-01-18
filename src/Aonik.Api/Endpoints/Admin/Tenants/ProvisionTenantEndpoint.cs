using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class ProvisionTenantEndpoint : EndpointWithoutRequest<ProvisionTenantResult>
{
    private readonly ITenantProvisioner _provisioner;

    public ProvisionTenantEndpoint(ITenantProvisioner provisioner)
    {
        _provisioner = provisioner;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/provision");
        Policies("Tenants.Write");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _provisioner.ProvisionTenantAsync(tenantId, ct);
        await Send.OkAsync(result, ct);
    }
}
