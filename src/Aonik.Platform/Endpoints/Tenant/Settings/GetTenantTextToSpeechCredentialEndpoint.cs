using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

internal sealed class GetTenantTextToSpeechCredentialEndpoint : EndpointWithoutRequest<TextToSpeechCredentialResponse>
{
    private readonly ITextToSpeechCredentialSettingsService _service;

    public GetTenantTextToSpeechCredentialEndpoint(ITextToSpeechCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/tenant/settings/text-to-speech/credentials");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetTenantAsync("ElevenLabs", ct);
        await Send.OkAsync(new TextToSpeechCredentialResponse(
            snapshot.Provider,
            snapshot.HasHostCredential,
            snapshot.HasTenantOverride,
            snapshot.EffectiveSource), ct);
    }
}
