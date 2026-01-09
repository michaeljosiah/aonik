using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Tenants;

public class GetTenantHealthEndpoint : EndpointWithoutRequest<TenantHealthResult>
{
    private readonly ITenantProvisioner _provisioner;

    public GetTenantHealthEndpoint(ITenantProvisioner provisioner)
    {
        _provisioner = provisioner;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}/health");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        
        var result = await _provisioner.CheckTenantHealthAsync(tenantId, ct);
        await Send.OkAsync(result, ct);
    }
}
