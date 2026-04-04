using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

internal sealed class UpdateTenantTextToSpeechCredentialEndpoint : Endpoint<TextToSpeechCredentialUpdateRequest, TextToSpeechCredentialResponse>
{
    private readonly ITextToSpeechCredentialSettingsService _service;

    public UpdateTenantTextToSpeechCredentialEndpoint(ITextToSpeechCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/tenant/settings/text-to-speech/credentials");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(TextToSpeechCredentialUpdateRequest req, CancellationToken ct)
    {
        var snapshot = await _service.SaveTenantAsync(new TextToSpeechCredentialUpdate(req.Provider, req.ApiKey, req.ClearStoredValue), ct);
        await Send.OkAsync(new TextToSpeechCredentialResponse(
            snapshot.Provider,
            snapshot.HasHostCredential,
            snapshot.HasTenantOverride,
            snapshot.EffectiveSource), ct);
    }
}
