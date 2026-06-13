using System.Diagnostics;
using System.Text.Json;

using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Capture;

/// <summary>
/// Parses a captured image / text / audio-transcript into a structured draft
/// proposal (Spec 047). Mirrors <c>CustomerInsightAiSummaryService</c>: a
/// one-shot <see cref="IChatClient.GetResponseAsync"/> constrained by a JSON
/// schema (<see cref="CaptureParseStructuredOutputContract"/>), bracketed by an
/// <c>AiRun</c> audit. It persists nothing — the returned draft is a proposal
/// the user must confirm; only Spec 045's create writes a record.
/// <para>
/// The vision lift (Spec 047 §5): for <c>inputType=image</c> the payload is
/// marshalled into a multimodal <see cref="ChatMessage"/> carrying a
/// <see cref="DataContent"/> image part alongside the text instruction — the
/// content path the text-only chat converter lacks. This is isolated to the
/// capture flow and does not touch the chat/stream converter.
/// </para>
/// </summary>
internal sealed class CaptureParseService : ICaptureParseService
{
    // A receipt photo base64 is comfortably under this; the validator bounds the
    // request earlier, this is the in-service backstop (Spec 047 §12 input safety).
    private const int MaxImageBytes = 6 * 1024 * 1024;

    private static readonly IReadOnlySet<string> AllowedImageMediaTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/gif", "image/webp",
        };

    private readonly IChatClient _chatClient;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ILogger<CaptureParseService> _logger;

    public CaptureParseService(
        IChatClient chatClient,
        IAiRunWriter aiRunWriter,
        ILogger<CaptureParseService> logger)
    {
        _chatClient = chatClient;
        _aiRunWriter = aiRunWriter;
        _logger = logger;
    }

    public async Task<CaptureParseResponse> ParseAsync(
        CaptureParseRequest request,
        CancellationToken cancellationToken = default)
    {
        var inputType = (request.InputType ?? string.Empty).Trim();

        // §8 — record the input SHAPE only, never the raw payload (image bytes / text).
        var inputRefsJson = JsonSerializer.Serialize(
            new { inputType },
            CaptureParseStructuredOutputContract.SerializerOptions);

        var aiRunId = await _aiRunWriter.StartRunAsync(
            CaptureParseStructuredOutputContract.UseCase,
            inputRefsJson,
            cancellationToken);

        try
        {
            List<ChatMessage> messages;
            try
            {
                messages = BuildMessages(request, inputType);
            }
            catch (CaptureParseInputException ex)
            {
                // Undecodable / unsupported image — capture never dead-ends. The
                // client falls back to the manual form with the raw attachment.
                _logger.LogInformation(
                    "Capture parse: input could not be marshalled ({Reason}); returning unparseable.", ex.Message);
                await _aiRunWriter.MarkRunCompletedWithMetricsAsync(
                    aiRunId, tokensUsed: 0, latencyMs: 0, costEstimate: 0m,
                    outputRef: $"capture:{CaptureParseStatuses.Unparseable}", cancellationToken: cancellationToken);
                return new CaptureParseResponse(CaptureParseStatuses.Unparseable, null);
            }

            var schema = JsonDocument.Parse(CaptureParseStructuredOutputContract.JsonSchema).RootElement;
            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema,
                    schemaName: CaptureParseStructuredOutputContract.SchemaName,
                    schemaDescription: CaptureParseStructuredOutputContract.SchemaDescription),
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    // Drives use-case routing to the vision-capable model (Spec 047 §5, O1)
                    // and tags the trace / audit run as capture_parse.
                    [AiTelemetry.UseCaseAttribute] = CaptureParseStructuredOutputContract.UseCase,
                },
            };

            var stopwatch = Stopwatch.StartNew();
            var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            stopwatch.Stop();

            var tokensUsed = (int)Math.Min(int.MaxValue, response.Usage?.TotalTokenCount ?? 0L);
            var result = ParseResponseText(response.Text);

            await _aiRunWriter.MarkRunCompletedWithMetricsAsync(
                aiRunId,
                tokensUsed,
                (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                costEstimate: 0m,
                outputRef: $"capture:{result.Status}",
                cancellationToken: cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            await TryMarkRunFailedAsync(aiRunId, ex.Message, cancellationToken);
            throw;
        }
    }

    private List<ChatMessage> BuildMessages(CaptureParseRequest request, string inputType)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, CaptureParseStructuredOutputContract.BuildSystemPrompt()),
        };

        var hintsJson = JsonSerializer.Serialize(
            request.Hints ?? new CaptureHints(null, null),
            CaptureParseStructuredOutputContract.SerializerOptions);

        var instruction =
            $"Parse the captured {inputType} into a paymentLog draft and match it against the user's hints.\n\n" +
            $"Hints (candidate entities + open commitments):\n{hintsJson}";

        if (string.Equals(inputType, CaptureInputTypes.Image, StringComparison.OrdinalIgnoreCase))
        {
            var (bytes, mediaType) = DecodeImage(request.Payload);
            messages.Add(new ChatMessage(ChatRole.User,
            [
                new TextContent(instruction + "\n\nThe captured image is attached."),
                new DataContent(bytes, mediaType),
            ]));
        }
        else
        {
            // text + audioTranscript share the text path (Spec 047 O3 — on-device STT).
            messages.Add(new ChatMessage(ChatRole.User,
                instruction + "\n\nCaptured text:\n" + (request.Payload ?? string.Empty)));
        }

        return messages;
    }

    /// <summary>
    /// Decodes a base64 (optionally <c>data:</c>-URI) image payload and resolves
    /// its media type, sniffing the magic bytes so a mislabelled payload is still
    /// classified correctly. Throws <see cref="CaptureParseInputException"/> for
    /// anything that is not a bounded, supported image — the caller maps that to
    /// an <c>unparseable</c> result rather than sending junk to the model.
    /// </summary>
    private static (byte[] Bytes, string MediaType) DecodeImage(string? payload)
    {
        var raw = (payload ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            throw new CaptureParseInputException("empty image payload");
        }

        string? dataUriMediaType = null;
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = raw.IndexOf(',');
            if (comma < 0)
            {
                throw new CaptureParseInputException("malformed data URI");
            }

            var header = raw[5..comma]; // between "data:" and ","
            var semicolon = header.IndexOf(';');
            dataUriMediaType = (semicolon >= 0 ? header[..semicolon] : header).Trim();
            raw = raw[(comma + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            throw new CaptureParseInputException("payload is not valid base64");
        }

        if (bytes.Length == 0)
        {
            throw new CaptureParseInputException("decoded image is empty");
        }

        if (bytes.Length > MaxImageBytes)
        {
            throw new CaptureParseInputException("image exceeds size limit");
        }

        var mediaType = SniffImageMediaType(bytes)
            ?? (AllowedImageMediaTypes.Contains(dataUriMediaType ?? string.Empty) ? dataUriMediaType : null);

        if (mediaType is null || !AllowedImageMediaTypes.Contains(mediaType))
        {
            throw new CaptureParseInputException("unsupported image content type");
        }

        return (bytes, mediaType);
    }

    /// <summary>Identifies the image type from its leading magic bytes; null if unrecognised.</summary>
    private static string? SniffImageMediaType(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    /// <summary>
    /// Deserialises the model's JSON into a <see cref="CaptureParseResponse"/>,
    /// normalising the status. Never throws — any malformed output collapses to
    /// <c>unparseable</c> so capture never dead-ends (Spec 047 §4).
    /// </summary>
    private CaptureParseResponse ParseResponseText(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new CaptureParseResponse(CaptureParseStatuses.Unparseable, null);
        }

        var json = StripJsonFences(responseText);

        CaptureParseResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CaptureParseResponse>(
                json, CaptureParseStructuredOutputContract.SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogInformation("Capture parse: model output was not valid JSON ({Message}); unparseable.", ex.Message);
            return new CaptureParseResponse(CaptureParseStatuses.Unparseable, null);
        }

        if (parsed is null)
        {
            return new CaptureParseResponse(CaptureParseStatuses.Unparseable, null);
        }

        return Normalize(parsed);
    }

    private static CaptureParseResponse Normalize(CaptureParseResponse parsed)
    {
        // No draft → unparseable regardless of what the model claimed.
        if (parsed.Draft is null)
        {
            return new CaptureParseResponse(CaptureParseStatuses.Unparseable, null);
        }

        var status = (parsed.Status ?? string.Empty).Trim();
        var normalizedStatus = status switch
        {
            _ when string.Equals(status, CaptureParseStatuses.Unparseable, StringComparison.OrdinalIgnoreCase)
                => CaptureParseStatuses.Unparseable,
            _ when string.Equals(status, CaptureParseStatuses.LowConfidence, StringComparison.OrdinalIgnoreCase)
                => CaptureParseStatuses.LowConfidence,
            _ => CaptureParseStatuses.Parsed,
        };

        // An "unparseable" status with a populated draft is contradictory — keep the draft, treat as low confidence.
        if (normalizedStatus == CaptureParseStatuses.Unparseable)
        {
            normalizedStatus = CaptureParseStatuses.LowConfidence;
        }

        var draft = parsed.Draft with
        {
            Kind = string.IsNullOrWhiteSpace(parsed.Draft.Kind) ? "paymentLog" : parsed.Draft.Kind,
            FieldConfidence = parsed.Draft.FieldConfidence ?? new Dictionary<string, double>(),
        };

        return new CaptureParseResponse(normalizedStatus, draft);
    }

    private async Task TryMarkRunFailedAsync(Guid aiRunId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture parse: failed to record AiRun failure for {AiRunId}", aiRunId);
        }
    }

    private static string StripJsonFences(string responseText)
    {
        var trimmed = responseText.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }

            trimmed = trimmed.Trim();
        }

        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }
}

/// <summary>
/// Internal signal that a capture payload could not be marshalled into a model
/// message (e.g. an undecodable or unsupported image). The service maps this to
/// an <c>unparseable</c> result rather than a failed run — capture never dead-ends.
/// </summary>
internal sealed class CaptureParseInputException : Exception
{
    public CaptureParseInputException(string message) : base(message) { }
}
