namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// Full graph definition for the editor canvas. The list page calls
/// <c>GET /workflows</c> and gets <see cref="WorkflowSummaryResponse"/>;
/// opening a workflow in the editor calls <c>GET /workflows/{slug}</c>
/// and gets this richer payload.
/// </summary>
public sealed record WorkflowGraphResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string State,
    string Version,
    bool AutoRetry,
    string OwnerColor,
    string OwnerName,
    IReadOnlyList<string> Contributors,
    IReadOnlyList<WorkflowGraphNode> Nodes,
    IReadOnlyList<WorkflowGraphEdge> Edges,
    IReadOnlyList<WorkflowGraphComment> Comments);

public sealed record WorkflowGraphNode(
    Guid Id,
    string Kind,
    string Label,
    string Summary,
    string Notes,
    int X,
    int Y,
    /// <summary>Per-kind parameters as raw JSON. UI knows the kind-specific shape.</summary>
    string ParamsJson);

public sealed record WorkflowGraphEdge(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    int FromIndex,
    string Label);

public sealed record WorkflowGraphComment(
    Guid Id,
    int X,
    int Y,
    string Author,
    string Body);
