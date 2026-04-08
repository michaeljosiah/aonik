using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal sealed class GetHostTextToSpeechCredentialEndpoint : Endpoint<GetTenantTextToSpeechCredentialRequest, TextToSpeechCredentialResponse>
{
    private readonly ITextToSpeechCredentialSettingsService _service;

    public GetHostTextToSpeechCredentialEndpoint(ITextToSpeechCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/settings/text-to-speech/credentials/host");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get host TTS credential status";
            s.Description = "Returns the status of the host-level text-to-speech API credential, including whether a key is stored and the effective source.";
            s.Response(200, "Credential status");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(GetTenantTextToSpeechCredentialRequest req, CancellationToken ct)
    {
        var provider = string.IsNullOrWhiteSpace(req.Provider) ? "ElevenLabs" : req.Provider;
        var snapshot = await _service.GetHostAsync(provider, ct);
        await Send.OkAsync(new TextToSpeechCredentialResponse(
            snapshot.Provider,
            snapshot.HasHostCredential,
            snapshot.HasTenantOverride,
            snapshot.EffectiveSource), ct);
    }
}
