namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Centralised OpenTelemetry constants for the AONIK AI subsystem.
/// All AI/Agent instrumentation shares these values so that the
/// ServiceDefaults trace/meter subscriptions and the module-level
/// instrumentation stay in sync.
/// </summary>
public static class AiTelemetry
{
    /// <summary>
    /// ActivitySource / Meter name used for all AONIK AI and Agent
    /// OpenTelemetry instrumentation. Subscribed to in ServiceDefaults
    /// via <c>AddSource("Aonik.Ai")</c> and <c>AddMeter("Aonik.Ai")</c>.
    /// </summary>
    public const string SourceName = "Aonik.Ai";

    /// <summary>
    /// Configuration key that controls whether sensitive data (prompts, responses,
    /// function call arguments, and results) is included in OpenTelemetry traces.
    /// Defaults to <c>false</c>. Only enable in development/testing environments.
    /// </summary>
    public const string EnableSensitiveDataKey = "AI:OpenTelemetry:EnableSensitiveData";

    /// <summary>
    /// Span attribute / baggage key used to propagate the conversation session ID
    /// to all child spans. Langfuse maps this to its Session concept for grouping
    /// multi-turn conversations.
    /// </summary>
    public const string SessionIdAttribute = "langfuse.session.id";

    /// <summary>
    /// Span attribute / baggage key used to propagate the authenticated user ID
    /// to all child spans. Langfuse maps this to its User concept for per-user
    /// analytics.
    /// </summary>
    public const string UserIdAttribute = "langfuse.user.id";

    public const string AiRunIdAttribute = "aonik.ai_run_id";

    public const string TraceObservationLogName = "AiTraceObservation";

    public const string ObservationIdAttribute = "aonik.observation.id";

    public const string ObservationTypeAttribute = "aonik.observation.type";

    public const string ObservationNameAttribute = "aonik.observation.name";

    public const string ObservationTraceNameAttribute = "aonik.observation.trace_name";
}
