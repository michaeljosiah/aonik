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
}
