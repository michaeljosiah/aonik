using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class GetGlobalSettingValueEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
{
    private readonly ISettingProvider _settingProvider;

    public GetGlobalSettingValueEndpoint(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/admin/settings/values/{key}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get a global setting value";
            s.Description = "Retrieves the current value of a global platform setting by its key.";
            s.Response(200, "Setting value");
            s.Response(401, "Not authenticated");
            s.Response(404, "Setting key not found");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(SettingKeyRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var value = await _settingProvider.GetForScopeAsync(req.Key, SettingScope.Global, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, value, "Global"), ct);
    }
}
