using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aonik.Ai.Observability;

/// <summary>
/// Run-level counters complementing the per-call telemetry on
/// <see cref="TelemetryChatClient"/>. Where the chat-client meter sits
/// at every individual LLM invocation, these counters fire once per
/// AiRun (i.e. once per logical "agent run" — what a user perceives as
/// a single conversational turn). Tagged with tenant.id so dashboards
/// can show per-tenant activity and token consumption without scraping
/// the AnkAiRuns table.
/// </summary>
/// <remarks>
/// Meter name "Aonik.Ai" is already registered in
/// <c>Aonik.ServiceDefaults.Extensions</c> for the chat-client
/// histograms; this class shares the same meter so the same exporters
/// pick the new counters up automatically.
/// </remarks>
public sealed class AiRunMetrics : IDisposable
{
    public const string MeterName = "Aonik.Ai";
    public const string MeterVersion = "1.0.0";

    private readonly Meter _meter;
    private readonly Counter<long> _runsCompleted;
    private readonly Counter<long> _runTokens;

    public AiRunMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);

        _runsCompleted = _meter.CreateCounter<long>(
            name: "aonik.ai.runs.completed",
            unit: "{run}",
            description: "Count of AiRun rows reaching a terminal outcome. Tagged with tenant.id, outcome (Completed/Failed), use_case.");

        _runTokens = _meter.CreateCounter<long>(
            name: "aonik.ai.runs.tokens_used",
            unit: "{token}",
            description: "Total tokens consumed across AiRuns. Sums to per-tenant token usage when grouped by tenant.id; per-call distribution stays on the aonik.ai.call.* histograms.");
    }

    /// <summary>
    /// Increment the runs-completed counter. <paramref name="tokensUsed"/>
    /// (when greater than zero) also feeds the per-tenant tokens-used
    /// counter. <paramref name="useCase"/> is the semantic label that the
    /// caller threads through <c>AiTelemetry.UseCaseAttribute</c> (e.g.
    /// "chat", "title-generation") so dashboards can break down by what
    /// kind of run consumed the budget.
    /// </summary>
    public void RecordRunCompleted(Guid tenantId, string outcome, string? useCase, long tokensUsed)
    {
        var tags = new TagList
        {
            { "tenant.id", tenantId.ToString() },
            { "outcome", string.IsNullOrWhiteSpace(outcome) ? "Unknown" : outcome },
            { "use_case", string.IsNullOrWhiteSpace(useCase) ? "unknown" : useCase },
        };

        _runsCompleted.Add(1, tags);

        if (tokensUsed > 0)
        {
            _runTokens.Add(tokensUsed, tags);
        }
    }

    public void Dispose() => _meter.Dispose();
}
