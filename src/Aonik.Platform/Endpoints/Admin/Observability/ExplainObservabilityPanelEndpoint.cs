using System.Text.Json;
using Aonik.Platform.Contracts.Api.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class ExplainObservabilityPanelEndpoint
    : Endpoint<ExplainObservabilityPanelRequest, ExplainObservabilityPanelResponse>
{
    private readonly IChatClient _chatClient;

    public ExplainObservabilityPanelEndpoint(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public override void Configure()
    {
        Post("/admin/observability/explain");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Explain a dashboard panel's current data";
            s.Description =
                "Produces a short plain-English summary of the given panel's current metrics, " +
                "written in speech-first form so it can be piped directly to TTS for playback.";
            s.Response(200, "Summary generated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(
        ExplainObservabilityPanelRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PanelKind))
        {
            ThrowError("PanelKind is required.");
        }

        var panelLabel = req.PanelKind.ToLowerInvariant() switch
        {
            "fleet" =>
                "Agent Fleet — live view of all AI agents with per-agent call counts, average latency, and total token usage.",
            "performance" =>
                "Performance Monitor — latency percentiles P50, P95, P99, time-to-first-token, client vs server timing breakdown, and token usage.",
            "cost" =>
                "Cost and Tokens — input and output token consumption broken down per agent.",
            "errors" =>
                "Errors and Failures — error rate, error time series, and top error groups.",
            _ => req.PanelKind,
        };

        var metricsJson =
            req.Metrics.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : req.Metrics.GetRawText();

        const string systemMessage = """
            You are summarizing an observability dashboard panel for a human admin. Write two to three short sentences in plain English, in speech-first form, so the text can be played as spoken audio without any post-processing.

            Rules:
            - No markdown, bullet points, numbered lists, or emojis.
            - Spell out acronyms so they are pronounced letter-by-letter: say "T T F T" not "TTFT", "L L M" not "LLM", "A P I" not "API".
            - Verbalize numbers and latencies: say "one point two seconds" not "1.2s", "ninety five percent" not "95%", "three thousand tokens" not "3K tokens".
            - State what the data shows and what it means. Call out anything elevated, unusual, or healthy. Be direct, no hedging.
            - If the metrics show no activity or the panel is not configured, say so plainly in one sentence.
            - Output ONLY the summary text. No preamble, no repetition of the rules, no closing remarks.
            """;

        var userMessage = $$"""
            Panel: {{panelLabel}}

            Current metrics (JSON):
            {{metricsJson}}
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, userMessage),
        };

        var response = await _chatClient.GetResponseAsync(
            messages,
            options: new ChatOptions { ModelId = "gpt-5-mini" },
            cancellationToken: ct);

        var summary = response.Text?.Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "No summary available right now.";
        }

        await Send.OkAsync(new ExplainObservabilityPanelResponse(summary), ct);
    }
}
