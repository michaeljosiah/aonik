using Aonik.SharedKernel.Abstractions.Ai.Speech;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin.Speech;

// Chat Speech active settings — singleton per tenant. Voice picker + playback ergonomics.

internal sealed class GetChatSpeechSettingsEndpoint : EndpointWithoutRequest<ChatSpeechSettings>
{
    private readonly IChatSpeechSettingsService _service;
    public GetChatSpeechSettingsEndpoint(IChatSpeechSettingsService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/chat-speech-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get Chat Speech active settings";
            s.Description = "Returns the current tenant's Chat Speech settings (active TTS provider, on/off, auto-play, show-speak-button, rate).";
            s.Response(200, "Settings (or defaults)");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.GetAsync(ct), ct);
}

internal sealed class UpdateChatSpeechSettingsEndpoint : Endpoint<UpdateChatSpeechSettingsRequest, ChatSpeechSettings>
{
    private readonly IChatSpeechSettingsService _service;
    public UpdateChatSpeechSettingsEndpoint(IChatSpeechSettingsService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/chat-speech-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Upsert Chat Speech active settings";
            s.Description = "Picks the active TTS provider and tweaks playback. Validates that the referenced provider exists, is type Tts, and is active. Rate must be in [50, 200].";
            s.Response(200, "Saved");
            s.Response(422, "Validation error (unknown provider, wrong type, disabled, or rate out of range)");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(UpdateChatSpeechSettingsRequest req, CancellationToken ct)
        => await Send.OkAsync(await _service.UpdateAsync(req, ct), ct);
}
