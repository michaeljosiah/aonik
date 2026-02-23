using Aonik.Api.Contracts.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Application.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Settings;

public class GetGlobalSettingValueEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
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
