using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update TTS settings";
            s.Description = "Updates the current tenant's text-to-speech configuration including provider, voice, model, and output format.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request or provider error");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
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
