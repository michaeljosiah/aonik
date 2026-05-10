using Aonik.SharedKernel.Abstractions.Ai.Speech;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin.Speech;

// Voice Mode active settings — singleton per tenant. GET returns the current settings (or
// defaults when no row exists yet); PUT upserts.

internal sealed class GetVoiceModeSettingsEndpoint : EndpointWithoutRequest<VoiceModeSettings>
{
    private readonly IVoiceModeSettingsService _service;
    public GetVoiceModeSettingsEndpoint(IVoiceModeSettingsService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/voice-mode-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get Voice Mode active settings";
            s.Description = "Returns the current tenant's Voice Mode settings (active recipe + on/off). Lazy default response when no row exists yet.";
            s.Response(200, "Settings (or defaults)");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.GetAsync(ct), ct);
}

internal sealed class UpdateVoiceModeSettingsEndpoint : Endpoint<UpdateVoiceModeSettingsRequest, VoiceModeSettings>
{
    private readonly IVoiceModeSettingsService _service;
    public UpdateVoiceModeSettingsEndpoint(IVoiceModeSettingsService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/voice-mode-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Upsert Voice Mode active settings";
            s.Description = "Picks the active recipe and enables/disables Voice Mode. Validates that the referenced recipe exists and is active.";
            s.Response(200, "Saved");
            s.Response(422, "Recipe id doesn't resolve, or referenced recipe is disabled");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(UpdateVoiceModeSettingsRequest req, CancellationToken ct)
        => await Send.OkAsync(await _service.UpdateAsync(req, ct), ct);
}
