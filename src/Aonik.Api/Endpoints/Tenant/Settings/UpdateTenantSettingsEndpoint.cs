using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Settings;

public class UpdateTenantSettingsEndpoint : Endpoint<UpdateTenantRequest, TenantResponse>
{
    private readonly ITenantService _tenantService;
    private readonly ITenantProvider _tenantProvider;

    public UpdateTenantSettingsEndpoint(ITenantService tenantService, ITenantProvider tenantProvider)
    {
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Patch("/tenant/settings");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(UpdateTenantRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var result = await _tenantService.UpdateTenantAsync(tenantId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
