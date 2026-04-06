using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class GetTenantTextToSpeechVoicesEndpoint : Endpoint<GetTextToSpeechVoicesRequest, List<TextToSpeechVoiceOptionResponse>>
{
    private readonly ITextToSpeechService _textToSpeechService;

    public GetTenantTextToSpeechVoicesEndpoint(ITextToSpeechService textToSpeechService)
    {
        _textToSpeechService = textToSpeechService;
    }

    public override void Configure()
    {
        Get("/tenant/settings/text-to-speech/voices");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List available TTS voices";
            s.Description = "Returns all available text-to-speech voices for the specified provider, including labels and preview URLs.";
            s.Response(200, "Success");
            s.Response(400, "Invalid provider");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(GetTextToSpeechVoicesRequest req, CancellationToken ct)
    {
        try
        {
            var voices = await _textToSpeechService.GetVoicesAsync(req.Provider, ct);
            var response = voices
                .Select(voice => new TextToSpeechVoiceOptionResponse(
                    voice.VoiceId,
                    voice.Name,
                    voice.PreviewUrl,
                    voice.Category,
                    new Dictionary<string, string?>(voice.Labels, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
