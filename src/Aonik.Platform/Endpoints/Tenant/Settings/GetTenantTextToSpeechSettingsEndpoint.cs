using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class GetTenantTextToSpeechSettingsEndpoint : EndpointWithoutRequest<TextToSpeechSettingsResponse>
{
    private readonly ITenantTextToSpeechSettingsService _service;

    public GetTenantTextToSpeechSettingsEndpoint(ITenantTextToSpeechSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/tenant/settings/text-to-speech");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get TTS settings";
            s.Description = "Returns the current tenant's text-to-speech configuration including provider, voice, and model settings.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetCurrentAsync(ct);
        await Send.OkAsync(TextToSpeechSettingsMappings.ToResponse(snapshot), ct);
    }
}
