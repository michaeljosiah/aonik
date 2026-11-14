using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Uses the same gateway as Aonik's typed connector tools.</summary>
internal sealed class ConnectorWorkflowAction(ConnectorActionDescriptor action, IConnectorService connectors,
    IEnumerable<IConnectorActionProvider> providers, WorkflowInvocationContext context) : IWorkflowActionHandler
{
    public WorkflowActionDescriptor Descriptor { get; } = new(action.Key, action.Version, action.ToolName, "Integration.Use",
        action.Approval, action.InputSchema, action.OutputSchema);
    public void ValidateArguments(JsonElement arguments) => providers.Single(provider => provider.ConnectorKey == action.ConnectorKey)
        .ValidateArguments(new(action.Key, action.Version), arguments);
    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        if (action.Approval is not null) throw new InvalidOperationException("Connector mutations require their durable dispatch adapter.");
        var run = context.Run ?? throw new InvalidOperationException("A leased invocation is required.");
        // The trusted reader independently checks this binding against the immutable version.
        var definition = context.Definition ?? throw new InvalidOperationException("A published definition is required.");
        var node = definition.Nodes.Single(node => node.Key == context.Step?.NodeKey);
        var parameters = node.Parameters.Deserialize<Contracts.Models.Workflows.WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
        Guid connectionId = parameters.Connection?.ConnectionId ?? throw new ConnectorAccessException("WorkflowBindingRequired", "Bind a connection before publishing.");
        try
        {
            return new("Completed", await connectors.InvokeWorkflowReadAsync(run.Id, invocation.StepRunId, connectionId,
                new(action.Key, action.Version), invocation.Arguments, cancellationToken));
        }
        catch (PermissionDeniedException denied) when (denied.PermissionKey.StartsWith("Integration.", StringComparison.Ordinal))
        { throw new ConnectorAccessException("NoAuthorizedConnection", "The recorded actor no longer has connector access."); }
    }
    public Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => Task.FromResult(new WorkflowActionReconciliation(null, action.Approval is null));
}
