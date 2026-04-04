using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class PreviewTenantTextToSpeechEndpoint : Endpoint<TextToSpeechPreviewRequest>
{
    private readonly ITextToSpeechService _textToSpeechService;

    public PreviewTenantTextToSpeechEndpoint(ITextToSpeechService textToSpeechService)
    {
        _textToSpeechService = textToSpeechService;
    }

    public override void Configure()
    {
        Post("/tenant/settings/text-to-speech/preview");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(TextToSpeechPreviewRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _textToSpeechService.SynthesizeAsync(
                new TextToSpeechSynthesisRequest(
                    req.Text,
                    req.Locale,
                    ThreadId: null,
                    MessageId: null,
                    UseCase: "platform.admin.tts.preview",
                    VoiceProfileOverride: new TextToSpeechVoiceProfile(
                        string.IsNullOrWhiteSpace(req.Provider) ? "ElevenLabs" : req.Provider.Trim(),
                        req.VoiceId ?? string.Empty,
                        req.ModelId,
                        req.Locale,
                        req.OutputFormat,
                        req.ProviderOptions ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))),
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
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
