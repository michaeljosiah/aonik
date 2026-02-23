using Aonik.Api.Contracts.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Application.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Settings;

public class UpdateTenantSettingValueEndpoint : Endpoint<SettingValueUpdateRequest, SettingValueResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ISettingManager _settingManager;

    public UpdateTenantSettingValueEndpoint(ITenantProvider tenantProvider, ISettingManager settingManager)
    {
        _tenantProvider = tenantProvider;
        _settingManager = settingManager;
    }

    public override void Configure()
    {
        Put("/tenant/settings/values/{key}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(SettingValueUpdateRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await _settingManager.SetAsync(req.Key, req.Value, SettingScope.Tenant, tenantId, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, req.Value, "Tenant"), ct);
    }
}
