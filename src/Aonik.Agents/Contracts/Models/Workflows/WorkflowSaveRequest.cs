namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// Editor save payload. Sent on POST (new) and PUT (update). The whole
/// graph is replaced on each save — server assigns fresh Guids for every
/// node and edge, and resolves edge endpoints by matching node
/// <see cref="WorkflowSaveNode.ClientId"/>s within this request. Clients
/// should refetch via GET after save to pick up the canonical Guids.
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
    string? VersionMessage);

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
    string ParamsJson);

public sealed record WorkflowSaveEdge(
    string FromClientId,
    string ToClientId,
    int FromIndex,
    string Label);
