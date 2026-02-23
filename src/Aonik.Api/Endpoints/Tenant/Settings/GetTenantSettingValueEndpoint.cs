using Aonik.Api.Contracts.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Application.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Settings;

public class GetTenantSettingValueEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ISettingProvider _settingProvider;

    public GetTenantSettingValueEndpoint(ITenantProvider tenantProvider, ISettingProvider settingProvider)
    {
        _tenantProvider = tenantProvider;
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/tenant/settings/values/{key}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(SettingKeyRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var value = await _settingProvider.GetForScopeAsync(req.Key, SettingScope.Tenant, tenantId, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, value, "Tenant"), ct);
    }
}
