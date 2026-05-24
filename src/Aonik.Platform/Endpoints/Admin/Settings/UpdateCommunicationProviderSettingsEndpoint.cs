using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class UpdateCommunicationProviderSettingsEndpoint
    : Endpoint<CommunicationProviderSettingsUpdateRequest, CommunicationProviderSettingsResponse>
{
    private readonly ICommunicationProviderSettingsService _service;

    public UpdateCommunicationProviderSettingsEndpoint(ICommunicationProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/settings/communication-provider");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update communication provider settings";
            s.Description =
                "Accepts per-channel update payloads for the implemented communication "
                + "provider connectors. Email and SMS active providers are stored separately; "
                + "secrets are write-only and reported back as Has* flags.";
            s.Response(200, "Settings updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CommunicationProviderSettingsUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var update = new CommunicationProviderSettingsUpdate(
                Email: req.Email == null
                    ? null
                    : new EmailChannelSettingsUpdate(
                        req.Email.ActiveProvider,
                        req.Email.AzureCommunicationServices == null
                            ? null
                            : new AzureEmailSettingsUpdate(
                                req.Email.AzureCommunicationServices.ConnectionString,
                                req.Email.AzureCommunicationServices.FromAddress)),
                Sms: req.Sms == null
                    ? null
                    : new SmsChannelSettingsUpdate(
                        req.Sms.ActiveProvider,
                        req.Sms.AzureCommunicationServices == null
                            ? null
                            : new AzureSmsSettingsUpdate(
                                req.Sms.AzureCommunicationServices.ConnectionString,
                                req.Sms.AzureCommunicationServices.FromPhoneNumber)));

            var snapshot = await _service.UpdateAsync(update, ct);
            await Send.OkAsync(GetCommunicationProviderSettingsEndpoint.MapResponse(snapshot), ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
