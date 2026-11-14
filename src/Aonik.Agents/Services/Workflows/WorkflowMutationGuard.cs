using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowMutationGuard(WorkflowDraftPreparation drafts, WorkflowGovernancePreparation governance) : IAgentMutationGuard
{
    public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<ToolGateContext> PrepareAsync(ToolGateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ToolName is "aonik_create_workflow" or "aonik_update_workflow")
        {
            RequireArguments(context, "draft");
            WorkflowSaveRequest draft = Read<WorkflowSaveRequest>(context, "draft");
            PreparedWorkflowDraft prepared = await drafts.PrepareAsync(draft, context.ToolName == "aonik_create_workflow", cancellationToken);
            context.Arguments["draft"] = JsonSerializer.SerializeToElement(prepared.Draft, WorkflowDefinitionCodec.JsonOptions);
            return context with { Preview = prepared.Preview };
        }
        if (context.ToolName == "aonik_delete_workflow")
        {
            RequireArguments(context, "slug", "expectedVersion");
            ToolApprovalPreview preview = await drafts.PrepareDeletionAsync(Read<string>(context, "slug"), Read<string>(context, "expectedVersion"), cancellationToken);
            return context with { Preview = preview };
        }
        if (context.ToolName is "aonik_request_publish_workflow" or "aonik_request_retire_workflow")
        {
            RequireArguments(context, "command");
            WorkflowGovernanceCommand command = Read<WorkflowGovernanceCommand>(context, "command");
            PreparedWorkflowGovernance prepared = await governance.PrepareAsync(command, context.ToolName == "aonik_request_retire_workflow", cancellationToken);
            context.Arguments["command"] = JsonSerializer.SerializeToElement(prepared.Command, WorkflowDefinitionCodec.JsonOptions);
            return context with { Preview = prepared.Preview };
        }
        return context;
    }

    private static T Read<T>(ToolGateContext context, string name) where T : class => context.Arguments.TryGetValue(name, out object? value)
        ? JsonSerializer.SerializeToElement(value, WorkflowDefinitionCodec.JsonOptions).Deserialize<T>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException($"{name} is required.")
        : throw new ArgumentException($"{name} is required.");

    private static void RequireArguments(ToolGateContext context, params string[] expected)
    {
        if (context.Arguments.Count != expected.Length || context.Arguments.Keys.Any(key => !expected.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Only the documented workflow tool arguments are accepted.");
    }
}
