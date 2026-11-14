using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Workflows;

internal sealed class ConnectorMutationGuard(IConnectorConnectionRequests requests, IConnectorMutationService? mutations = null) : IAgentMutationGuard
{
    public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task<ToolGateContext> PrepareAsync(ToolGateContext context, CancellationToken cancellationToken = default)
    {
        if (GmailActionPolicies.ActionForTool(context.ToolName) is { } actionKey)
        {
            if (mutations is null) throw new InvalidOperationException("The enforcing connector mutation service is unavailable.");
            Require(context, "connectionId", "request");
            Guid connectionId = Read<Guid>(context, "connectionId");
            context.Arguments["connectionId"] = connectionId;
            var action = new ConnectorActionRef(actionKey, 1);
            System.Text.Json.JsonElement arguments = GmailActionContracts.Normalize(action, ConnectorJson.Element(context.Arguments["request"]));
            context.Arguments["request"] = arguments;
            if (actionKey == GmailActionKeys.PrepareMessage)
                return context with { AuditArgumentsJson = ConnectorJson.Serialize(new { connectionId, actionKey,
                    requestHash = ToolArgumentsHash.Compute(context.Arguments) }) };
            ConnectorMutationPreview preview = await mutations.PreviewAsync(connectionId, action, arguments, cancellationToken);
            return context with { Preview = preview.Preview, InvocationKey = preview.InvocationKey };
        }
        if (context.ToolName == "aonik_create_connection_request")
        {
            Require(context, "request");
            CreateConnectionRequest request = Read<CreateConnectionRequest>(context, "request");
            ConnectionRequestPreview preview = await requests.PreviewAsync(request, cancellationToken);
            context.Arguments["request"] = ConnectorJson.Element(request with { Purpose = preview.Purpose, Actions = preview.Actions,
                ConnectionId = request.ConnectionId, WorkflowId = preview.WorkflowId, RunId = preview.RunId });
            return context with { Preview = new("Request connector access", null, ConnectorJson.Element(preview)) };
        }
        if (context.ToolName == "aonik_cancel_connection_request")
        {
            Require(context, "requestId", "expectedVersion");
            Guid id = Read<Guid>(context, "requestId");
            string token = Read<string>(context, "expectedVersion");
            ConnectionRequestDetail request = await requests.GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Connection request not found.");
            if (request.ExpectedVersion != token || request.Status is not ("Pending" or "AwaitingConsent")) throw new ArgumentException("Reload the pending request before cancelling.");
            return context with { Preview = new("Cancel connection request", ConnectorJson.Element(request), ConnectorJson.Element(new { request.Id, status = "Cancelled" })) };
        }
        return context;
    }
    private static T Read<T>(ToolGateContext context, string name) => ConnectorJson.Read<T>(ConnectorJson.Element(context.Arguments[name]));
    private static void Require(ToolGateContext context, params string[] expected)
    {
        if (context.Arguments.Count != expected.Length || context.Arguments.Keys.Any(key => !expected.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Only the documented connector tool arguments are accepted.");
    }
}
