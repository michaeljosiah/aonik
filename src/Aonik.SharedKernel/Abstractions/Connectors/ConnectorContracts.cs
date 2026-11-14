using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record ConnectorActionRef(string Key, int Version);
public sealed record ConnectorActionDescriptor(string ConnectorKey, string Key, int Version, string ToolName,
    IReadOnlyList<string> RequiredScopes, ToolApprovalOptions? Approval, JsonElement InputSchema, JsonElement OutputSchema);
public sealed record ConnectorDescriptor(string Key, string DisplayName, IReadOnlyList<ConnectorActionDescriptor> Actions);
public interface IConnectorDescriptor { ConnectorDescriptor Descriptor { get; } }

public sealed record ConnectorUseContext(WorkflowActor Actor, Guid? WorkflowId = null, Guid? WorkflowVersionId = null,
    Guid? RunId = null, Guid? StepRunId = null, Guid? PinnedGrantId = null, IReadOnlyList<ConnectorActionRef>? AllowedActions = null);
public sealed record WorkflowConnectorBinding(Guid ConnectionId, Guid WorkflowGrantId, IReadOnlyList<ConnectorActionRef> AllowedActions);
public sealed record ConnectorReadiness(string Code, string NextAction, Guid? ConnectionId = null, string? DisplayName = null);
public sealed record ConnectorConnectionSummary(Guid Id, string ConnectorKey, string Ownership, string DisplayName,
    string Status, IReadOnlyList<string> GrantedScopes, string ExpectedVersion, Guid? OwnerUserId);

/// <summary>Cross-module gateway. Actor and workflow identity come from server context, never model arguments.</summary>
public interface IConnectorService
{
    Task<IReadOnlyList<ConnectorDescriptor>> ListConnectorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectorConnectionSummary>> ListConnectionsAsync(string connectorKey, CancellationToken cancellationToken = default);
    Task<ConnectorReadiness> GetReadinessAsync(string connectorKey, ConnectorActionRef action, Guid? connectionId = null,
        CancellationToken cancellationToken = default);
    Task<JsonElement> InvokeReadAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default);
    Task<JsonElement> InvokeWorkflowReadAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action, JsonElement arguments,
        CancellationToken cancellationToken = default);
    Task ValidateWorkflowBindingAsync(Guid workflowId, WorkflowConnectorBinding binding, ConnectorActionRef action, WorkflowActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>Verifies the actual leased step and its published connection binding in the Agents module.</summary>
public interface IWorkflowConnectorContextReader
{
    Task<ConnectorUseContext> RequireAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action,
        JsonElement arguments, CancellationToken cancellationToken = default);
    Task RequireGrantTargetAsync(Guid workflowId, Guid userId, CancellationToken cancellationToken = default);
    Task<WorkflowConnectorRequestTarget> GetRequestTargetAsync(Guid runId, Guid userId, CancellationToken cancellationToken = default);
}
public sealed record WorkflowConnectorRequestTarget(Guid WorkflowId, Guid ConnectionId, Guid WorkflowGrantId,
    IReadOnlyList<ConnectorActionRef> AllowedActions);

public interface IConnectorPreferenceValidator
{
    Task RequireEligibleAsync(Guid connectionId, Guid userId, CancellationToken cancellationToken = default);
}
