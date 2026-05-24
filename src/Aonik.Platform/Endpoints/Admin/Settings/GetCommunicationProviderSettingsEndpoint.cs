using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class GetCommunicationProviderSettingsEndpoint
    : EndpointWithoutRequest<CommunicationProviderSettingsResponse>
{
    private readonly ICommunicationProviderSettingsService _service;

    public GetCommunicationProviderSettingsEndpoint(ICommunicationProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/settings/communication-provider");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get communication provider settings";
            s.Description =
                "Returns the active outbound-messaging provider plus the current state of each "
                + "channel's configuration (whether the connection string is set, from-address, "
                + "from-phone). Used by SettingsCommunicationPage in the Admin UI. Mirrors the "
                + "Auth provider snapshot pattern — secrets are reported via Has* indicators "
                + "rather than round-tripped.";
            s.Response(200, "Communication provider settings");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    internal static CommunicationProviderSettingsResponse MapResponse(
        Aonik.Platform.Contracts.Models.Settings.CommunicationProviderSettingsSnapshot snapshot)
    {
        return new CommunicationProviderSettingsResponse(
            snapshot.ActiveProvider,
            new AzureCommunicationSettingsResponse(
                snapshot.Azure.HasConnectionString,
                snapshot.Azure.EmailFromAddress,
                snapshot.Azure.SmsFromPhoneNumber));
    }
}
