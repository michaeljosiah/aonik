using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class GetTenantVoiceSettingsEndpoint : EndpointWithoutRequest<VoiceProviderSettingsResponse>
{
    private readonly ITenantVoiceProviderSettingsService _service;

    public GetTenantVoiceSettingsEndpoint(ITenantVoiceProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/tenant/settings/voice");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get voice provider settings";
            s.Description = "Returns the current tenant's voice provider configuration (recipe, vendor settings, kill-switch).";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var config = await _service.GetCurrentAsync(ct);
        await Send.OkAsync(VoiceSettingsMappings.ToResponse(config), ct);
    }
}
