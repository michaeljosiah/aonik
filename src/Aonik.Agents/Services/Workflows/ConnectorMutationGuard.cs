using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.Agents.Services.Connectors.Catalog;

namespace Aonik.Agents.Services.Workflows;

internal sealed class ConnectorMutationGuard(IConnectorConnectionRequests requests, IConnectorMutationService? mutations = null,
    CuratedConnectorCatalog? curated = null) : IAgentMutationGuard
{
    public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task<ToolGateContext> PrepareAsync(ToolGateContext context, CancellationToken cancellationToken = default)
    {
        string? gmail = GmailActionPolicies.ActionForTool(context.ToolName);
        ConnectorPackageActionContract? remote = curated?.FindAction(context.ToolName);
        if (gmail is not null || remote?.Descriptor.Approval is not null)
        {
            if (mutations is null) throw new InvalidOperationException("The enforcing connector mutation service is unavailable.");
            Require(context, "connectionId", "request");
            Guid connectionId = Read<Guid>(context, "connectionId");
            context.Arguments["connectionId"] = connectionId;
            var action = remote is null ? new ConnectorActionRef(gmail!, 1) : new(remote.Descriptor.Key, remote.Descriptor.Version);
            System.Text.Json.JsonElement arguments = ConnectorJson.Element(context.Arguments["request"]);
            if (remote is null) arguments = GmailActionContracts.Normalize(action, arguments);
            else remote.RequireInput(arguments);
            context.Arguments["request"] = arguments;
            if (action.Key == GmailActionKeys.PrepareMessage)
                return context with { AuditArgumentsJson = ConnectorJson.Serialize(new { connectionId, actionKey = action.Key,
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
