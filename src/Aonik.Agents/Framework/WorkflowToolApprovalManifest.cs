using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

internal sealed class WorkflowToolApprovalManifest : IToolApprovalManifest
{
    public const string PublishType = "Agents.PublishWorkflow";
    public const string RetireType = "Agents.RetireWorkflow";
    public string Module => "Agents";

    public ToolClassification? Classify(string name) => name switch
    {
        "aonik_list_workflows" or "aonik_get_workflow" or "aonik_get_workflow_capabilities"
            or "aonik_validate_workflow" or "aonik_simulate_workflow" or "aonik_get_workflow_run" => ToolClassification.ReadOnly,
        "aonik_create_workflow" or "aonik_update_workflow" or "aonik_delete_workflow" or "aonik_start_workflow"
            => ToolClassification.Mutating(new(ToolApprovalTier.Medium, name)),
        "aonik_request_publish_workflow" => ToolClassification.Mutating(PublicationOptions(false)),
        "aonik_request_retire_workflow" => ToolClassification.Mutating(PublicationOptions(true)),
        _ => null,
    };

    internal static ToolApprovalOptions PublicationOptions(bool retiring) => new(ToolApprovalTier.High,
        retiring ? "Retire a published workflow" : "Publish an exact workflow definition", retiring ? RetireType : PublishType);
}
