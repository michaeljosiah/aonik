using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class UpdateGlobalSettingValueEndpoint : Endpoint<SettingValueUpdateRequest, SettingValueResponse>
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
        Summary(s =>
        {
            s.Summary = "Update a global setting value";
            s.Description = "Sets or updates the value of a global platform setting by its key.";
            s.Response(200, "Setting updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Setting key not found");
        });
        Options(x => x.WithTags("Settings"));
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
