using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class UpdateTenantVoiceCredentialRouteRequest
{
    public string Provider { get; set; } = "OpenAI";
    public string? ApiKey { get; set; }
    public bool ClearStoredValue { get; set; }
}

internal sealed class UpdateTenantVoiceCredentialEndpoint : Endpoint<UpdateTenantVoiceCredentialRouteRequest, VoiceProviderCredentialResponse>
{
    private readonly IVoiceProviderCredentialSettingsService _service;

    public UpdateTenantVoiceCredentialEndpoint(IVoiceProviderCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/tenant/settings/voice/credentials/{Provider}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update voice provider credential";
            s.Description = "Stores or clears the tenant's API key for the given voice provider. Returns status only — never echoes the value back.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(UpdateTenantVoiceCredentialRouteRequest req, CancellationToken ct)
    {
        var snapshot = await _service.SaveTenantAsync(
            new VoiceProviderCredentialUpdate(
                Provider: req.Provider,
                ApiKey: req.ApiKey,
                ClearStoredValue: req.ClearStoredValue),
            ct);

        await Send.OkAsync(new VoiceProviderCredentialResponse(
            Provider: snapshot.Provider,
            HasHostCredential: snapshot.HasHostCredential,
            HasTenantOverride: snapshot.HasTenantOverride,
            EffectiveSource: snapshot.EffectiveSource), ct);
    }
}
