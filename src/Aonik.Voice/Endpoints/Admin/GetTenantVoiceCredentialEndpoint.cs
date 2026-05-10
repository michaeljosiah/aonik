using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class GetTenantVoiceCredentialRequest
{
    public string Provider { get; set; } = "OpenAI";
}

internal sealed class GetTenantVoiceCredentialEndpoint : Endpoint<GetTenantVoiceCredentialRequest, VoiceProviderCredentialResponse>
{
    private readonly IVoiceProviderCredentialSettingsService _service;

    public GetTenantVoiceCredentialEndpoint(IVoiceProviderCredentialSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/tenant/settings/voice/credentials/{Provider}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get voice provider credential status";
            s.Description = "Returns whether a host or tenant credential exists for the provider. Never echoes the raw API key.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(GetTenantVoiceCredentialRequest req, CancellationToken ct)
    {
        var snapshot = await _service.GetTenantAsync(req.Provider, ct);
        await Send.OkAsync(new VoiceProviderCredentialResponse(
            Provider: snapshot.Provider,
            HasHostCredential: snapshot.HasHostCredential,
            HasTenantOverride: snapshot.HasTenantOverride,
            EffectiveSource: snapshot.EffectiveSource), ct);
    }
}
