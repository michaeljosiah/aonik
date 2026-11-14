namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// Editor save payload. Sent on POST (new) and PUT (update). The whole
/// Stable node keys and existing identifiers survive edits. Updates require the
/// opaque concurrency token returned with the graph. State and version are server-owned.
/// </summary>
public sealed record WorkflowSaveRequest(
    string Slug,
    string Name,
    string Description,
    string State,
    string Version,
    bool AutoRetry,
    string OwnerColor,
    Guid? OwnerAgentId,
    IReadOnlyList<Guid> Contributors,
    IReadOnlyList<WorkflowSaveNode> Nodes,
    IReadOnlyList<WorkflowSaveEdge> Edges,
    string? VersionMessage,
    string? ExpectedVersion = null,
    string DefinitionKind = "Workflow");

public sealed record WorkflowSaveNode(
    /// <summary>Identifier the client uses to reference this node in
    /// <see cref="WorkflowSaveEdge.FromClientId"/> and
    /// <see cref="WorkflowSaveEdge.ToClientId"/>. May be a server Guid (for
    /// unchanged nodes) or a transient client id like "n3xa9f".</summary>
    string ClientId,
    string Kind,
    string Label,
    string Summary,
    string Notes,
    int X,
    int Y,
    string ParamsJson,
    string? Key = null);

public sealed record WorkflowSaveEdge(
    string FromClientId,
    string ToClientId,
    int FromIndex,
    string Label,
    string? RouteKey = null);
