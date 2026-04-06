using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal sealed class UpdateHostTextToSpeechCredentialEndpoint : Endpoint<TextToSpeechCredentialUpdateRequest, TextToSpeechCredentialResponse>
{
    private readonly ITextToSpeechCredentialSettingsService _service;

    public UpdateHostTextToSpeechCredentialEndpoint(ITextToSpeechCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/settings/text-to-speech/credentials/host");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update host TTS credential";
            s.Description = "Saves or clears the host-level text-to-speech API key used by the platform.";
            s.Response(200, "Credential updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(TextToSpeechCredentialUpdateRequest req, CancellationToken ct)
    {
        var snapshot = await _service.SaveHostAsync(new TextToSpeechCredentialUpdate(req.Provider, req.ApiKey, req.ClearStoredValue), ct);
        await Send.OkAsync(new TextToSpeechCredentialResponse(
            snapshot.Provider,
            snapshot.HasHostCredential,
            snapshot.HasTenantOverride,
            snapshot.EffectiveSource), ct);
    }
}
