using System.Text.Json;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class ExplainObservabilityPanelEndpoint
    : Endpoint<ExplainObservabilityPanelRequest, ExplainObservabilityPanelResponse>
{
    private const string UseCase = "observability_panel_explanation";
    private const string PromptName = "observability_panel_explanation";
    private const string DefaultModelId = "gpt-5-mini";

    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;

    public ExplainObservabilityPanelEndpoint(
        IChatClient chatClient,
        IAiTaskProfileResolver profileResolver)
    {
        _chatClient = chatClient;
        _profileResolver = profileResolver;
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
            "trace" =>
                "AI Trace Insights — one trace's span hierarchy, duration hotspots, errors, model usage, token counts, and notable child operations.",
            "errors" =>
                "Errors and Failures — error rate, error time series, and top error groups.",
            "overview" =>
                "Platform Overview — total inbound request volume, requests per minute, overall error rate as a percentage, and P95 response latency across the platform, with time-series charts for each metric.",
            "ai" =>
                "AI Performance — LLM call latency (P50 and P95), time-to-first-token, total token consumption, estimated cost broken down per model and use case, and per-agent execution statistics.",
            "dependencies" =>
                "External Dependencies — HTTP, SQL, gRPC, queue, and event hub dependency calls with success rates, average duration, total call counts, and failure counts.",
            "jobs" =>
                "Background Jobs — Quartz.NET job execution counts, success and failure rates, and average execution duration per registered job class.",
            "retrieval" =>
                "Vector Retrieval — Qdrant search and upsert operation counts, embedding generation calls, embedding error count, search latency percentiles per instrument, and per-collection search statistics.",
            "topology" =>
                "Service Topology — directed graph of service-to-service calls derived from distributed traces, showing call volume, error rate, and P95 latency per edge between services.",
            _ => req.PanelKind,
        };

        var metricsJson =
            req.Metrics.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : req.Metrics.GetRawText();

        var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, DefaultModelId, ct);
        var userMessage = (profile.UserPromptTemplate ?? """
            Panel: {{PANEL_LABEL}}

            Current metrics (JSON):
            {{METRICS_JSON}}
            """)
            .Replace("{{PANEL_LABEL}}", panelLabel, StringComparison.Ordinal)
            .Replace("{{METRICS_JSON}}", metricsJson, StringComparison.Ordinal);

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

        var response = await _chatClient.GetResponseAsync(
            messages,
            options: options,
            cancellationToken: ct);

        var summary = response.Text?.Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "No summary available right now.";
        }

        await Send.OkAsync(new ExplainObservabilityPanelResponse(summary), ct);
    }
}
