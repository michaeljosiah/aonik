using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerMutationGuard(WorkflowTriggerService triggers) : IAgentMutationGuard
{
    public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<ToolGateContext> PrepareAsync(ToolGateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ToolName is not ("aonik_save_workflow_trigger" or "aonik_delete_workflow_trigger"
            or "aonik_request_activate_workflow_trigger" or "aonik_request_disable_workflow_trigger")) return context;
        string third = context.ToolName == "aonik_save_workflow_trigger" ? "request"
            : context.ToolName == "aonik_delete_workflow_trigger" ? "expectedVersion" : "command";
        string[] expected = ["slug", "bindingId", third];
        if (context.Arguments.Count != expected.Length || context.Arguments.Keys.Any(key => !expected.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Only documented trigger tool arguments are accepted.");
        string slug = Read<string>(context, "slug");
        Guid? bindingId = JsonSerializer.SerializeToElement(context.Arguments["bindingId"]).Deserialize<Guid?>();
        if (context.ToolName == "aonik_save_workflow_trigger")
        {
            var prepared = await triggers.PrepareSaveAsync(slug, bindingId, Read<SaveWorkflowTriggerRequest>(context, third), cancellationToken);
            context.Arguments[third] = JsonSerializer.SerializeToElement(prepared.Request, WorkflowDefinitionCodec.JsonOptions);
            return context with { Preview = prepared.Preview };
        }
        if (bindingId is null || bindingId == Guid.Empty) throw new ArgumentException("A saved binding ID is required.");
        ToolApprovalPreview preview = context.ToolName == "aonik_delete_workflow_trigger"
            ? await triggers.PrepareDeletionAsync(slug, bindingId.Value, Read<string>(context, third), cancellationToken)
            : await triggers.PrepareGovernanceAsync(slug, bindingId.Value, Read<WorkflowTriggerCommand>(context, third),
                context.ToolName == "aonik_request_disable_workflow_trigger", cancellationToken);
        return context with { Preview = preview };
    }

    private static T Read<T>(ToolGateContext context, string name) where T : class
        => JsonSerializer.SerializeToElement(context.Arguments[name], WorkflowDefinitionCodec.JsonOptions).Deserialize<T>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException($"{name} is required.");
}
