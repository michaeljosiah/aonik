using System.Text.Json;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace Aonik.Platform.Endpoints.Admin.Observability;

/// <summary>
/// "Interpret this trace with AI" — admin-only endpoint that takes a
/// trace ID plus its observation rows and produces a structured analysis
/// (story, latency hotspots, errors, completeness gaps, opportunities).
///
/// The shape of the prompt is intentionally analytical rather than
/// speech-first: the output is rendered as readable text in the trace
/// detail slide-out, not played back. We borrow the same plumbing as
/// <see cref="ExplainObservabilityPanelEndpoint"/> — same chat client,
/// same task profile resolver, same use_case-stamped telemetry.
/// </summary>
internal sealed class ExplainTraceEndpoint
    : Endpoint<ExplainTraceRequest, ExplainTraceResponse>
{
    private const string UseCase = "trace_analysis";
    private const string PromptName = "trace_analysis";
    private const string DefaultModelId = "gpt-5-mini";

    // Cap the spans we send to the LLM. Long traces can easily exceed
    // the model's context window; we keep the longest spans and the
    // root, which is what the analysis cares about anyway.
    private const int MaxSpansSent = 60;

    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;

    public ExplainTraceEndpoint(
        IChatClient chatClient,
        IAiTaskProfileResolver profileResolver)
    {
        _chatClient = chatClient;
        _profileResolver = profileResolver;
    }

    public override void Configure()
    {
        Post("/admin/observability/traces/explain");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Interpret a trace with AI";
            s.Description =
                "Produces a structured analysis of a single distributed trace — a one-paragraph " +
                "story, latency hotspots, errors, completeness gaps, and concrete improvement " +
                "opportunities. Designed for admin diagnosis, not for speech.";
            s.Response(200, "Analysis generated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(ExplainTraceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TraceId))
        {
            ThrowError("TraceId is required.");
        }

        var spans = req.Spans.ValueKind is JsonValueKind.Array
            ? TrimSpansForLlm(req.Spans)
            : "[]";

        var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, DefaultModelId, ct);

        var userMessage = (profile.UserPromptTemplate ?? DefaultUserTemplate)
            .Replace("{{TRACE_ID}}", req.TraceId, StringComparison.Ordinal)
            .Replace("{{SPAN_COUNT}}", req.Spans.ValueKind == JsonValueKind.Array
                ? req.Spans.GetArrayLength().ToString()
                : "0", StringComparison.Ordinal)
            .Replace("{{SPANS_JSON}}", spans, StringComparison.Ordinal);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, userMessage),
        };

        if (!string.IsNullOrWhiteSpace(profile.SystemPrompt))
        {
            messages.Insert(0, new ChatMessage(ChatRole.System, profile.SystemPrompt));
        }

        var options = new ChatOptions
        {
            ModelId = profile.ModelId ?? DefaultModelId,
        };

        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties[AiTelemetry.UseCaseAttribute] = UseCase;

        string analysis;
        try
        {
            var response = await _chatClient.GetResponseAsync(
                messages,
                options: options,
                cancellationToken: ct);

            analysis = (response.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(analysis))
            {
                analysis = "The model returned an empty response. Try again — if it persists, the prompt may have been filtered or the model is unavailable.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller-cancelled; let the framework surface 499 / closed
            // connection as usual.
            throw;
        }
        catch (Exception ex)
        {
            // Surface a useful description to the UI instead of a bare
            // 500. The frontend renders the analysis text — pulling the
            // exception message into the same channel keeps admins in
            // the loop without making them open a separate logs view.
            var detail = ex.Message;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = ex.GetType().Name;
            }
            analysis =
                "AI analysis failed.\n\n" +
                $"Error: {detail}\n\n" +
                "This is usually a transient model availability or context-size issue. Click 'Interpret with AI' again to retry — the trace data itself is still intact in the waterfall below.";
        }

        await Send.OkAsync(new ExplainTraceResponse(analysis), ct);
    }

    /// <summary>
    /// Reduces the span payload to the most informative rows. Drops
    /// huge metadata blobs and trims chatty SQL bodies so we stay
    /// inside the model's context budget on big traces. Lifts the
    /// <c>error.*</c> tags out of <c>metadata</c> into top-level fields
    /// so the analyser can read exception type, message, and a short
    /// stack-trace excerpt without parsing arbitrary JSON.
    /// </summary>
    private static string TrimSpansForLlm(JsonElement spansArray)
    {
        var trimmed = new List<object>();
        var sorted = spansArray.EnumerateArray()
            .Select(span => (Span: span, Duration: TryGetDouble(span, "durationMs")))
            .OrderByDescending(x => x.Duration ?? 0)
            .Take(MaxSpansSent)
            .Select(x => x.Span);

        foreach (var span in sorted)
        {
            var (errorType, errorMessage, errorStackPreview) = ExtractErrorTagsFromMetadata(span);

            trimmed.Add(new
            {
                name = TryGetString(span, "name"),
                type = TryGetString(span, "type"),
                level = TryGetString(span, "level"),
                durationMs = TryGetDouble(span, "durationMs"),
                latencySeconds = TryGetDouble(span, "latencySeconds"),
                providedModel = TryGetString(span, "providedModel"),
                inputTokens = TryGetNumber(span, "inputTokens"),
                outputTokens = TryGetNumber(span, "outputTokens"),
                totalTokens = TryGetNumber(span, "totalTokens"),
                costUsd = TryGetDouble(span, "costUsd"),
                ttftSeconds = TryGetDouble(span, "timeToFirstTokenSeconds"),
                parentSpanId = TryGetString(span, "parentSpanId"),
                spanId = TryGetString(span, "spanId"),
                traceName = TryGetString(span, "traceName"),
                inputPreview = TruncateString(TryGetString(span, "input"), 240),
                outputPreview = TruncateString(TryGetString(span, "output"), 240),
                errorType,
                errorMessage,
                errorStackPreview,
            });
        }

        return JsonSerializer.Serialize(trimmed);
    }

    /// <summary>
    /// Pulls <c>error.type</c>, <c>error.message</c>, and a short
    /// <c>error.stacktrace</c> excerpt out of the span's
    /// <c>metadata</c> JSON. Returns nulls when the metadata is missing
    /// or doesn't carry error tags. Stack-trace excerpt is capped so a
    /// 4 KB stack doesn't dominate the model's context window.
    /// </summary>
    private static (string? Type, string? Message, string? StackPreview) ExtractErrorTagsFromMetadata(JsonElement span)
    {
        var metadataRaw = TryGetString(span, "metadata");
        if (string.IsNullOrWhiteSpace(metadataRaw))
        {
            return (null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataRaw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null);
            }

            var type = TryReadStringProperty(root, "error.type");
            var message = TryReadStringProperty(root, "error.message");
            var stack = TryReadStringProperty(root, "error.stacktrace");
            return (type, message, TruncateString(stack, 600));
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    private static string? TryReadStringProperty(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? TryGetDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;
    }

    private static long? TryGetNumber(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var l) ? l : null;
    }

    private static string? TruncateString(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= max ? value : value[..max] + "…";
    }

    private const string DefaultUserTemplate = """
        Trace ID: {{TRACE_ID}}
        Total spans observed: {{SPAN_COUNT}}

        Spans (top {{SPAN_COUNT}}, longest first, JSON):
        {{SPANS_JSON}}
        """;
}
