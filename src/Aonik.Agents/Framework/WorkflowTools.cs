using System.ComponentModel;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Framework;

internal sealed class WorkflowTools(IWorkflowService workflows, IWorkflowGovernanceService governance)
{
    [Description("List authorized tenant workflow metadata, with bounded pagination.")]
    public async Task<object> ListWorkflows(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var items = await workflows.ListAsync(skip, take, cancellationToken);
        return new { items, nextOffset = items.Count == take ? (int?)(skip + take) : null };
    }

    [Description("Read a workflow draft with stable keys and its expectedVersion concurrency token. Optional history is metadata only.")]
    public async Task<object?> GetWorkflow(string slug, bool includeHistory = false, CancellationToken cancellationToken = default)
    {
        WorkflowGraphResponse? workflow = await workflows.GetBySlugAsync(slug, cancellationToken);
        if (workflow is null) return null;
        return new { workflow, versions = includeHistory ? await workflows.ListVersionsAsync(workflow.Id, cancellationToken) : null };
    }

    [Description("Discover available workflow nodes, invoice fact fields/operators and the typed Decision parameters JSON schema. Unsupported executors cannot publish.")]
    public Task<WorkflowCapabilities> GetCapabilities(CancellationToken cancellationToken = default) => workflows.GetCapabilitiesAsync(cancellationToken);

    [Description("Validate a saved draft for publication. Returns exact definition hash and diagnostics; does not publish or execute it.")]
    public Task<WorkflowValidation?> ValidateWorkflow(string slug, CancellationToken cancellationToken = default) => workflows.ValidateAsync(slug, cancellationToken);

    [Description("Simulate a saved workflow using fixture invoice facts and action outputs only. This never invokes a live model, domain write, connector or notification.")]
    public Task<WorkflowSimulation?> SimulateWorkflow(string slug, WorkflowSimulationRequest fixture, CancellationToken cancellationToken = default)
        => workflows.SimulateAsync(slug, fixture, cancellationToken);

    [Description("Propose a new workflow draft. Read the capability/schema tool first. The server shows the exact change and requires human confirmation before saving. A duplicate slug conflicts.")]
    public Task<WorkflowGraphResponse> CreateWorkflow(WorkflowSaveRequest draft, CancellationToken cancellationToken = default)
        => workflows.CreateAsync(draft, cancellationToken);

    [Description("Propose replacement of an existing draft using its current expectedVersion and stable keys. The server requires human confirmation; stale drafts conflict.")]
    public Task<WorkflowGraphResponse?> UpdateWorkflow(WorkflowSaveRequest draft, CancellationToken cancellationToken = default)
        => workflows.UpdateAsync(draft, cancellationToken);

    [Description("Propose deletion of an unused unpublished draft using its expectedVersion. Published or executed workflows require retirement instead. Requires human confirmation.")]
    public Task<bool> DeleteWorkflow(string slug, string expectedVersion, CancellationToken cancellationToken = default)
        => workflows.DeleteAsync(slug, expectedVersion, cancellationToken);

    [Description("Request publication of the exact validated draft hash and expected version/published pointer. Optional targetVersionId requests rollback to a previously published snapshot. Queues a high-risk governance proposal; never activates inline.")]
    public Task<WorkflowGovernanceRequestResult> RequestPublication(WorkflowGovernanceCommand command, CancellationToken cancellationToken = default)
        => governance.RequestAsync(command, retiring: false, cancellationToken);

    [Description("Request retirement of the exact published hash/pointer. Queues a high-risk governance proposal; never retires inline.")]
    public Task<WorkflowGovernanceRequestResult> RequestRetirement(WorkflowGovernanceCommand command, CancellationToken cancellationToken = default)
        => governance.RequestAsync(command, retiring: true, cancellationToken);

    public static IEnumerable<AITool> CreateAll(IServiceProvider services)
    {
        IWorkflowService? workflows = services.GetService<IWorkflowService>();
        IWorkflowGovernanceService? governance = services.GetService<IWorkflowGovernanceService>();
        if (workflows is null || governance is null) return [];
        var tools = new WorkflowTools(workflows, governance);
        AITool[] functions =
        [
            AIFunctionFactory.Create(tools.ListWorkflows, name: "aonik_list_workflows"),
            AIFunctionFactory.Create(tools.GetWorkflow, name: "aonik_get_workflow"),
            AIFunctionFactory.Create(tools.GetCapabilities, name: "aonik_get_workflow_capabilities"),
            AIFunctionFactory.Create(tools.ValidateWorkflow, name: "aonik_validate_workflow"),
            AIFunctionFactory.Create(tools.SimulateWorkflow, name: "aonik_simulate_workflow"),
            AIFunctionFactory.Create(tools.CreateWorkflow, name: "aonik_create_workflow"),
            AIFunctionFactory.Create(tools.UpdateWorkflow, name: "aonik_update_workflow"),
            AIFunctionFactory.Create(tools.DeleteWorkflow, name: "aonik_delete_workflow"),
            AIFunctionFactory.Create(tools.RequestPublication, name: "aonik_request_publish_workflow"),
            AIFunctionFactory.Create(tools.RequestRetirement, name: "aonik_request_retire_workflow"),
        ];
        return services.GetRequiredService<IToolApprovalGate>().GateAll(functions, services);
    }
}
