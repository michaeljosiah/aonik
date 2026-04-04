using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

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
