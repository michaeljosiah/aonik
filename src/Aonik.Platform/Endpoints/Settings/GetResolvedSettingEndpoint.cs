using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get resolved setting value";
            s.Description = "Resolves a setting by key using the full cascade (user, tenant, global) and returns the effective value and source.";
            s.Response(200, "Success");
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

        var resolution = await _settingProvider.GetResolvedAsync(req.Key, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, resolution.Value, resolution.Source), ct);
    }
}
