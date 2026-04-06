using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

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
        Summary(s =>
        {
            s.Summary = "Update tenant settings";
            s.Description = "Partially updates the current tenant's profile settings such as name, branding, or configuration.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(UpdateTenantRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var result = await _tenantService.UpdateTenantAsync(tenantId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
