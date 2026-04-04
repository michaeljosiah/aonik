using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal sealed class GetHostTextToSpeechCredentialEndpoint : EndpointWithoutRequest<TextToSpeechCredentialResponse>
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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetHostAsync("ElevenLabs", ct);
        await Send.OkAsync(new TextToSpeechCredentialResponse(
            snapshot.Provider,
            snapshot.HasHostCredential,
            snapshot.HasTenantOverride,
            snapshot.EffectiveSource), ct);
    }
}
