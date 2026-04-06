using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class GetTenantSettingsEndpoint : EndpointWithoutRequest<TenantResponse>
{
    private readonly ITenantService _tenantService;
    private readonly ITenantProvider _tenantProvider;

    public GetTenantSettingsEndpoint(ITenantService tenantService, ITenantProvider tenantProvider)
    {
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/tenant/settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant settings";
            s.Description = "Returns the full settings profile for the current tenant, including name, branding, and configuration.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var result = await _tenantService.GetTenantAsync(tenantId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
