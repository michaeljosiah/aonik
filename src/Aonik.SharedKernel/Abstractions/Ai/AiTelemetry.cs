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
}
