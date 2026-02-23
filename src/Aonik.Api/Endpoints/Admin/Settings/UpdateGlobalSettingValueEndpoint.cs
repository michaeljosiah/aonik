using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Settings;

public class UpdateGlobalSettingValueEndpoint : Endpoint<SettingValueUpdateRequest, SettingValueResponse>
{
    private readonly ISettingManager _settingManager;

    public UpdateGlobalSettingValueEndpoint(ISettingManager settingManager)
    {
        _settingManager = settingManager;
    }

    public override void Configure()
    {
        Put("/admin/settings/values/{key}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(SettingValueUpdateRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _settingManager.SetAsync(req.Key, req.Value, SettingScope.Global, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, req.Value, "Global"), ct);
    }
}
