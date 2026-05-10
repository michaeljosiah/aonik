using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Configuration;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class UpdateTenantVoiceSettingsEndpoint : Endpoint<VoiceProviderSettingsUpdateRequest, VoiceProviderSettingsResponse>
{
    private readonly ITenantVoiceProviderSettingsService _service;
    private readonly IVoiceProviderConfigurationValidator _validator;

    public UpdateTenantVoiceSettingsEndpoint(
        ITenantVoiceProviderSettingsService service,
        IVoiceProviderConfigurationValidator validator)
    {
        _service = service;
        _validator = validator;
    }

    public override void Configure()
    {
        Put("/tenant/settings/voice");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update voice provider settings";
            s.Description = "Persists the supplied voice configuration after validation. v1 accepts only chained kind with OpenAI vendors.";
            s.Response(200, "Success");
            s.Response(400, "Validation error");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(VoiceProviderSettingsUpdateRequest req, CancellationToken ct)
    {
        var config = VoiceSettingsMappings.FromUpdate(req);
        var validation = _validator.Validate(config);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                AddError(error);
            }
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var saved = await _service.SaveCurrentAsync(config, ct);
        await Send.OkAsync(VoiceSettingsMappings.ToResponse(saved), ct);
    }
}
