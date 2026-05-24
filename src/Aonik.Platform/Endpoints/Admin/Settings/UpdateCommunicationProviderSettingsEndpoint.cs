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
                "Accepts an update payload for symmetry with the auth provider endpoint. The "
                + "service currently rejects writes (returns 400 with a 'configuration-managed' "
                + "message) — operators set the underlying Communication:* keys via appsettings "
                + "/ environment variables.";
            s.Response(200, "Settings updated (reserved — not currently reachable)");
            s.Response(400, "Configuration-managed; updates rejected");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CommunicationProviderSettingsUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var update = new CommunicationProviderSettingsUpdate(
                req.ActiveProvider,
                req.Azure == null
                    ? null
                    : new AzureCommunicationSettingsUpdate(
                        req.Azure.ConnectionString,
                        req.Azure.EmailFromAddress,
                        req.Azure.SmsFromPhoneNumber));

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
