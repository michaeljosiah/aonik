using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update TTS credentials";
            s.Description = "Sets or clears the tenant-level text-to-speech API key for the specified provider.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
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
