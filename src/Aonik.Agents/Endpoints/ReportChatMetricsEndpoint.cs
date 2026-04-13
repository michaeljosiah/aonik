using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Request DTO for client-reported chat performance metrics.
/// Defined here (not in Platform contracts) because Aonik.Agents does not
/// reference Aonik.Platform. The data flows through to App Insights via
/// structured logging — no shared contract is needed.
/// </summary>
public record ChatClientMetricsRequest
{
    /// <summary>Client-measured total round-trip time in milliseconds (send → last token).</summary>
    public long ClientRoundTripMs { get; init; }

    /// <summary>Client-measured time to first token in milliseconds (send → first text delta).</summary>
    public long ClientTtftMs { get; init; }

    /// <summary>Server-reported latency from the RUN_FINISHED metrics.</summary>
    public long ServerLatencyMs { get; init; }

    /// <summary>Server-reported TTFT from the RUN_FINISHED metrics.</summary>
    public long ServerTtftMs { get; init; }

    /// <summary>Input token count from server metrics.</summary>
    public long InputTokens { get; init; }

    /// <summary>Output token count from server metrics.</summary>
    public long OutputTokens { get; init; }

    /// <summary>AG-UI thread ID for correlation.</summary>
    public string? ThreadId { get; init; }

    /// <summary>AG-UI run ID for correlation.</summary>
    public string? RunId { get; init; }

    /// <summary>Agent name (e.g. "personal-finance-agent").</summary>
    public string? AgentName { get; init; }
}

/// <summary>
/// Lightweight endpoint for mobile/web clients to report chat performance
/// metrics after an AG-UI run completes. Emits the data as structured log
/// entries so they flow through OpenTelemetry to App Insights for dashboard
/// visualisation.
///
/// Fire-and-forget from the client perspective — always returns 204.
/// </summary>
internal sealed class ReportChatMetricsEndpoint : Endpoint<ChatClientMetricsRequest>
{
    public override void Configure()
    {
        Post("/api/chat/metrics");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Report client-side chat metrics";
            s.Description = "Accepts client-measured performance metrics for a completed AG-UI run.";
            s.Response(204, "Accepted");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ChatClientMetricsRequest req, CancellationToken ct)
    {
        Logger.LogInformation(
            "ChatClientMetrics: ThreadId={ThreadId} RunId={RunId} AgentName={AgentName} ClientRoundTripMs={ClientRoundTripMs} ClientTtftMs={ClientTtftMs} ServerLatencyMs={ServerLatencyMs} ServerTtftMs={ServerTtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens}",
            req.ThreadId, req.RunId, req.AgentName,
            req.ClientRoundTripMs, req.ClientTtftMs,
            req.ServerLatencyMs, req.ServerTtftMs,
            req.InputTokens, req.OutputTokens);

        await Send.NoContentAsync(ct);
    }
}
