using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetCurrentAsync(ct);
        await Send.OkAsync(TextToSpeechSettingsMappings.ToResponse(snapshot), ct);
    }
}
