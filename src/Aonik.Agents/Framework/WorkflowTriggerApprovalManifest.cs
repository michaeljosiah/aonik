using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

internal sealed class WorkflowTriggerApprovalManifest : IToolApprovalManifest
{
    public const string ActivateType = "Agents.ActivateWorkflowTrigger";
    public const string DisableType = "Agents.DisableWorkflowTrigger";
    public string Module => "Agents";
    public ToolClassification? Classify(string name) => name switch
    {
        "aonik_list_workflow_triggers" or "aonik_get_workflow_trigger" or "aonik_validate_workflow_trigger" => ToolClassification.ReadOnly,
        "aonik_save_workflow_trigger" or "aonik_delete_workflow_trigger" => ToolClassification.Mutating(new(ToolApprovalTier.Medium, name)),
        "aonik_request_activate_workflow_trigger" => ToolClassification.Mutating(Options(false)),
        "aonik_request_disable_workflow_trigger" => ToolClassification.Mutating(Options(true)),
        _ => null,
    };
    internal static bool IsGovernanceType(string type) => type is ActivateType or DisableType;
    internal static bool IsGovernanceTool(string name) => name is "aonik_request_activate_workflow_trigger" or "aonik_request_disable_workflow_trigger";
    internal static bool IsDraftTool(string name) => name is "aonik_save_workflow_trigger" or "aonik_delete_workflow_trigger";
    internal static ToolApprovalOptions Options(bool disabling) => new(ToolApprovalTier.High,
        disabling ? "Disable an automatic workflow trigger" : "Activate an exact workflow trigger", disabling ? DisableType : ActivateType);
}
