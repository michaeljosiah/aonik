using System.ComponentModel;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

internal sealed class WorkflowTriggerTools(IWorkflowTriggerService triggers)
{
    [Description("List this workflow's automatic trigger drafts and active revisions. Activation is a separate high-risk human-approved proposal.")]
    public Task<IReadOnlyList<WorkflowTriggerDetail>> ListTriggers(string slug, CancellationToken cancellationToken = default)
        => triggers.ListAsync(slug, cancellationToken);

    [Description("Read an automatic trigger, its exact configuration hashes and expectedVersion concurrency token.")]
    public Task<WorkflowTriggerDetail?> GetTrigger(string slug, Guid bindingId, CancellationToken cancellationToken = default)
        => triggers.GetAsync(slug, bindingId, cancellationToken);

    [Description("Check a saved trigger's pinned published version, automation identity grants, schedule and connection readiness. Does not activate or execute it.")]
    public Task<WorkflowTriggerValidation> ValidateTrigger(string slug, Guid bindingId, CancellationToken cancellationToken = default)
        => triggers.ValidateAsync(slug, bindingId, cancellationToken);

    [Description("Propose saving an automatic trigger draft. Use null bindingId for a new draft; edits require its current expectedVersion. Human confirmation is required. This never changes an active revision.")]
    public Task<WorkflowTriggerDetail> SaveTrigger(string slug, Guid? bindingId, SaveWorkflowTriggerRequest request, CancellationToken cancellationToken = default)
        => triggers.SaveAsync(slug, bindingId, request, cancellationToken);

    [Description("Propose deleting an unused trigger draft with its expectedVersion. Requires human confirmation. Previously activated bindings retain history and must be disabled.")]
    public async Task<object> DeleteTrigger(string slug, Guid bindingId, string expectedVersion, CancellationToken cancellationToken = default)
    {
        await triggers.DeleteAsync(slug, bindingId, expectedVersion, cancellationToken);
        return new { deleted = true };
    }

    [Description("Request activation of the exact saved trigger hash and expectedVersion, including its pinned workflow/actor, subject scope and bounds. Always queues a high-risk human governance proposal; never activates inline.")]
    public Task<WorkflowTriggerGovernanceResult> ActivateTrigger(string slug, Guid bindingId, WorkflowTriggerCommand command, CancellationToken cancellationToken = default)
        => triggers.RequestAsync(slug, bindingId, command, false, cancellationToken);

    [Description("Request disabling an active trigger using its exact active hash and expectedVersion. Queues a high-risk human governance proposal; queued effects revalidate the binding.")]
    public Task<WorkflowTriggerGovernanceResult> DisableTrigger(string slug, Guid bindingId, WorkflowTriggerCommand command, CancellationToken cancellationToken = default)
        => triggers.RequestAsync(slug, bindingId, command, true, cancellationToken);

    public static IEnumerable<AITool> CreateAll(IServiceProvider services)
    {
        IWorkflowTriggerService? triggers = services.GetService<IWorkflowTriggerService>();
        if (triggers is null) return [];
        var tools = new WorkflowTriggerTools(triggers);
        return services.GetRequiredService<IToolApprovalGate>().GateAll([
            AIFunctionFactory.Create(tools.ListTriggers, name: "aonik_list_workflow_triggers"),
            AIFunctionFactory.Create(tools.GetTrigger, name: "aonik_get_workflow_trigger"),
            AIFunctionFactory.Create(tools.ValidateTrigger, name: "aonik_validate_workflow_trigger"),
            AIFunctionFactory.Create(tools.SaveTrigger, name: "aonik_save_workflow_trigger"),
            AIFunctionFactory.Create(tools.DeleteTrigger, name: "aonik_delete_workflow_trigger"),
            AIFunctionFactory.Create(tools.ActivateTrigger, name: "aonik_request_activate_workflow_trigger"),
            AIFunctionFactory.Create(tools.DisableTrigger, name: "aonik_request_disable_workflow_trigger"),
        ], services);
    }
}
