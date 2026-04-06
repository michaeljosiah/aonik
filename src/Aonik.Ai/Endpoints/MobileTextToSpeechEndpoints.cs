using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

public record MobileTextToSpeechSynthesisRequest(
    string SpeechText,
    string? Locale,
    string? ThreadId,
    string? MessageId);

internal sealed class MobileTextToSpeechSynthesizeEndpoint : Endpoint<MobileTextToSpeechSynthesisRequest>
{
    private readonly ITextToSpeechService _textToSpeechService;

    public MobileTextToSpeechSynthesizeEndpoint(ITextToSpeechService textToSpeechService)
    {
        _textToSpeechService = textToSpeechService;
    }

    public override void Configure()
    {
        Post("/mobile/text-to-speech/synthesize");
        Summary(s =>
        {
            s.Summary = "Synthesize text to speech audio";
            s.Description = "Converts text to speech audio using the configured TTS provider. Returns an audio stream with provider metadata headers.";
            s.Response(200, "Audio stream returned");
            s.Response(400, "Invalid request or policy violation");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(MobileTextToSpeechSynthesisRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _textToSpeechService.SynthesizeAsync(
                new TextToSpeechSynthesisRequest(
                    req.SpeechText,
                    req.Locale,
                    req.ThreadId,
                    req.MessageId),
                ct);

            await using var audioStream = result.AudioStream;
            using var resource = result.ResourceToDispose;

            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            HttpContext.Response.ContentType = result.ContentType;
            HttpContext.Response.Headers.Append("X-Ai-Run-Id", result.AiRunId.ToString("D"));
            HttpContext.Response.Headers.Append("X-Tts-Provider", result.Provider);
            HttpContext.Response.Headers.Append("X-Tts-Voice-Id", result.VoiceId);

            await audioStream.CopyToAsync(HttpContext.Response.Body, ct);
            await HttpContext.Response.Body.FlushAsync(ct);
        }
        catch (Aonik.Ai.Services.TextToSpeechPolicyViolationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(502, ct);
        }
    }
}
