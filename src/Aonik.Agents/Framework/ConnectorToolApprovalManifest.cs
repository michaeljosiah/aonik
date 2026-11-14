using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Framework;

internal sealed class ConnectorToolApprovalManifest : IToolApprovalManifest
{
    public string Module => "Platform";
    public ToolClassification? Classify(string name) => GmailActionPolicies.ActionForTool(name) is { } action
        ? GmailActionPolicies.Approval(action) is { } options ? ToolClassification.Mutating(options) : ToolClassification.ReadOnly
        : name switch
    {
        "aonik_list_connectors" or "aonik_list_connections" or "aonik_get_connection_readiness" or "aonik_get_connection_request"
            or "aonik_list_connection_requests" or "gmail_search_messages" or "gmail_get_message" or "gmail_get_attachment"
            or "gmail_get_mutation_status" or "gmail_reconcile_mutation" => ToolClassification.ReadOnly,
        "aonik_create_connection_request" or "aonik_cancel_connection_request" => ToolClassification.Mutating(new(ToolApprovalTier.Medium, name)),
        _ => null,
    };
}
