using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Settings;

public class GetResolvedSettingEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
{
    private readonly ISettingProvider _settingProvider;

    public GetResolvedSettingEndpoint(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/v1/settings/resolved/{key}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SettingKeyRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var resolution = await _settingProvider.GetResolvedAsync(req.Key, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, resolution.Value, resolution.Source), ct);
    }
}
