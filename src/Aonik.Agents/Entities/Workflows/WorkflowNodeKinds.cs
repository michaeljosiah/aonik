namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Allowed values for <see cref="WorkflowNode.Kind"/>. Mirrors NODE_KIND in
/// templates/aonik-admin-starterkit/screens/workflow-editor.jsx so the API
/// + DB + UI step-kind catalogue stay aligned.
/// </summary>
public static class WorkflowNodeKinds
{
    public const string Trigger = "Trigger";
    public const string Tool = "Tool";
    public const string Agent = "Agent";
    public const string Decision = "Decision";
    public const string Human = "Human";
    public const string Wait = "Wait";
    public const string Notify = "Notify";
    public const string Emit = "Emit";
    public const string Loop = "Loop";
    public const string End = "End";

    /// <summary>
    /// The subset of kinds the graph runtime (<c>GraphWorkflowBuilder</c>) can
    /// actually execute today. The remaining editor kinds (<see cref="Tool"/>,
    /// <see cref="Decision"/>, <see cref="Loop"/>, <see cref="Human"/>,
    /// <see cref="Wait"/>) are valid catalogue/editor vocabulary with deferred
    /// executors — a graph using them saves and renders in the editor fine, but
    /// cannot be run via <c>POST /ai/workflows/run</c> until its executor lands.
    /// The run path gates on this set so an unrunnable graph fails fast with a
    /// clear message rather than executing partway (firing real notify/emit side
    /// effects) and then throwing at the first unsupported node.
    /// <para><see cref="Trigger"/> is included because it is a legal, runnable
    /// node — it is virtual (no executor) but its presence never blocks a run.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> RuntimeSupported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Trigger,
            Agent,
            Notify,
            Emit,
            End,
        };

    /// <summary>
    /// True when <paramref name="kind"/> is a kind the graph runtime can execute
    /// (see <see cref="RuntimeSupported"/>). Case-insensitive; null or blank is false.
    /// </summary>
    public static bool IsRuntimeSupported(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && RuntimeSupported.Contains(kind);
}
