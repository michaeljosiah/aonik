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
                "Returns Email + SMS channels independently — each carries its own active "
                + "provider plus per-provider credentials (with secrets reported via Has* "
                + "indicators rather than round-tripped). Used by SettingsCommunicationPage "
                + "in the Admin UI.";
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
            Email: new EmailChannelSettingsResponse(
                ActiveProvider: snapshot.Email.ActiveProvider,
                AzureCommunicationServices: snapshot.Email.AzureCommunicationServices == null
                    ? null
                    : new AzureEmailSettingsResponse(
                        snapshot.Email.AzureCommunicationServices.HasConnectionString,
                        snapshot.Email.AzureCommunicationServices.FromAddress)),
            Sms: new SmsChannelSettingsResponse(
                ActiveProvider: snapshot.Sms.ActiveProvider,
                AzureCommunicationServices: snapshot.Sms.AzureCommunicationServices == null
                    ? null
                    : new AzureSmsSettingsResponse(
                        snapshot.Sms.AzureCommunicationServices.HasConnectionString,
                        snapshot.Sms.AzureCommunicationServices.FromPhoneNumber)));
    }
}
