using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Settings;

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
