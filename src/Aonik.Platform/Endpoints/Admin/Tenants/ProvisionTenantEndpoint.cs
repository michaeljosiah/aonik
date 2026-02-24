using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _provisioner.ProvisionTenantAsync(tenantId, ct);
        await Send.OkAsync(result, ct);
    }
}
