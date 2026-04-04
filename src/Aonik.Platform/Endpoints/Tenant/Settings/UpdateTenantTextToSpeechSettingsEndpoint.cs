using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class UpdateTenantTextToSpeechSettingsEndpoint : Endpoint<TextToSpeechSettingsUpdate, TextToSpeechSettingsResponse>
{
    private readonly ITenantTextToSpeechSettingsService _service;

    public UpdateTenantTextToSpeechSettingsEndpoint(ITenantTextToSpeechSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/tenant/settings/text-to-speech");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(TextToSpeechSettingsUpdate req, CancellationToken ct)
    {
        try
        {
            var updated = await _service.SaveCurrentAsync(TextToSpeechSettingsMappings.ToSettings(req), ct);
            await Send.OkAsync(TextToSpeechSettingsMappings.ToResponse(updated), ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
