namespace Aonik.SharedKernel.Abstractions.Ai;

using System.Diagnostics;

/// <summary>
/// Centralised OpenTelemetry constants for the AONIK AI subsystem.
/// All AI/Agent instrumentation shares these values so that the
/// ServiceDefaults trace/meter subscriptions and the module-level
/// instrumentation stay in sync.
/// </summary>
public static class AiTelemetry
{
    public static readonly ActivitySource ActivitySource = new(SourceName);

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

    public const string UseCaseAttribute = "aonik.use_case";

    public const string AiRunIdAttribute = "aonik.ai_run_id";

    /// <summary>
    /// Marker set on a streaming call's <c>ChatOptions.AdditionalProperties</c> by a caller that
    /// already persists its own AiRun for the stream (AG-UI / voice, via
    /// <c>PostStreamPersistenceCoordinator</c>). The audit middleware skips its own streaming
    /// audit when this is present, so a streaming turn produces exactly one AiRun row (H14).
    /// </summary>
    public const string StreamAuditHandledDownstreamAttribute = "aonik.stream_audit_handled_downstream";

    public const string TraceObservationLogName = "AiTraceObservation";

    public const string ObservationIdAttribute = "aonik.observation.id";

    public const string ObservationTypeAttribute = "aonik.observation.type";

    public const string ObservationNameAttribute = "aonik.observation.name";

    public const string ObservationTraceNameAttribute = "aonik.observation.trace_name";

    /// <summary>
    /// Maximum number of characters of <c>error.stacktrace</c> stamped on
    /// an activity. Set to ~4 KB so the captured frames cover the
    /// immediate fault site without bloating the span payload (Application
    /// Insights charges per byte and trace explorers struggle with very
    /// large customDimensions blobs).
    /// </summary>
    public const int MaxStacktraceCharacters = 4_000;

    /// <summary>
    /// Marks <paramref name="activity"/> as failed and stamps three
    /// diagnostic tags so the trace explorer can render meaningful error
    /// detail without re-fetching:
    /// <list type="bullet">
    ///   <item><c>error.type</c> — exception runtime type name. For
    ///     cancellations this is the concrete derived type (e.g.
    ///     <c>TaskCanceledException</c>) not the base
    ///     <c>OperationCanceledException</c>.</item>
    ///   <item><c>error.message</c> — exception message, with a synthetic
    ///     fallback for cancellations whose message is empty (older
    ///     framework runtimes throw OCE with a null message).</item>
    ///   <item><c>error.stacktrace</c> — exception stack trace truncated to
    ///     <see cref="MaxStacktraceCharacters"/>, so we keep the immediate
    ///     fault frames without exploding span size.</item>
    /// </list>
    /// </summary>
    public static void MarkError(Activity? activity, Exception exception)
    {
        if (activity is null) return;

        var (errorType, errorMessage) = ResolveErrorTypeAndMessage(exception);

        activity.SetStatus(ActivityStatusCode.Error, errorMessage);
        activity.SetTag("error.type", errorType);
        activity.SetTag("error.message", errorMessage);

        var stack = exception.StackTrace;
        if (!string.IsNullOrWhiteSpace(stack))
        {
            activity.SetTag("error.stacktrace", TruncateStacktrace(stack));
        }
    }

    /// <summary>
    /// Resolves the exception type name and a useful message. Cancellations
    /// (<see cref="OperationCanceledException"/> and its derivatives) often
    /// arrive with an empty message; we synthesise one so the trace
    /// explorer doesn't show an empty <c>error.message</c> tag.
    /// </summary>
    private static (string Type, string Message) ResolveErrorTypeAndMessage(Exception exception)
    {
        var typeName = exception.GetType().Name;

        if (exception is OperationCanceledException
            && string.IsNullOrWhiteSpace(exception.Message))
        {
            return (typeName, "operation cancelled — likely timeout");
        }

        return (typeName, exception.Message ?? string.Empty);
    }

    private static string TruncateStacktrace(string stack)
    {
        return stack.Length <= MaxStacktraceCharacters
            ? stack
            : stack[..MaxStacktraceCharacters];
    }
}
