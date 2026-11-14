using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Internal request-port contract, never an agent-invocable financial tool.</summary>
internal static class WorkflowHumanTaskContract
{
    public const string Key = "workflow.human-response";
    public const string ToolName = "workflow_respond_human_task";
    public static WorkflowActionDescriptor Descriptor { get; } = new(Key, 1, ToolName, "Workflow.Run",
        new(ToolApprovalTier.Medium, "Respond to workflow task"),
        JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }));

    public static JsonElement Arguments(WorkflowRun run, WorkflowStepRun step, WorkflowHumanParameters parameters)
        => JsonSerializer.SerializeToElement(new { runId = run.Id, stepRunId = step.Id, run.DefinitionHash, run.SubjectId,
            deadlineUtc = DateTime.SpecifyKind(step.DeadlineUtc!.Value, DateTimeKind.Utc), parameters }, WorkflowDefinitionCodec.JsonOptions);

    public static WorkflowHumanParameters Parameters(WorkflowDefinitionNode node)
    {
        WorkflowHumanParameters parameters = node.Parameters.Deserialize<WorkflowHumanParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("A typed human task is required.");
        if (parameters.SchemaVersion != 1 || string.IsNullOrWhiteSpace(parameters.Purpose) || parameters.Purpose.Length > 240
            || parameters.Reviewer is null || parameters.Reviewer.UserId.HasValue == parameters.Reviewer.RoleId.HasValue
            || parameters.Reviewer.UserId == Guid.Empty || parameters.Reviewer.RoleId == Guid.Empty
            || parameters.TimeoutSeconds is < 1 or > WorkflowControlLimits.MaximumLifetimeSeconds
            || parameters.Choices is null || parameters.Choices.Count is < 1 or > 10
            || parameters.Choices.Any(choice => choice is null || !WorkflowDefinitionCodec.IsKey(choice.Key)
                || string.IsNullOrWhiteSpace(choice.Label) || choice.Label.Length > 100 || !WorkflowDefinitionCodec.IsKey(choice.Route))
            || parameters.Choices.Select(choice => choice.Key).Distinct(StringComparer.Ordinal).Count() != parameters.Choices.Count
            || !WorkflowDefinitionCodec.IsKey(parameters.RejectRoute) || !WorkflowDefinitionCodec.IsKey(parameters.TimeoutRoute))
            throw new ArgumentException("Use a purpose, one current user or role, bounded choices and deadline, and explicit rejection/timeout routes.");
        return parameters;
    }

    public static string ValidateResponse(WorkflowHumanParameters parameters, ToolApprovalDecisionInput decision)
    {
        if (decision.Decision == ToolApprovalDecisionType.Reject)
        {
            if (decision.Response.HasValue) throw new ArgumentException("Rejected tasks cannot carry a response.");
            return string.Empty;
        }
        if (decision.Response is not { ValueKind: JsonValueKind.Object } response || response.EnumerateObject().Count() != 1
            || !response.TryGetProperty("choice", out JsonElement choice) || choice.ValueKind != JsonValueKind.String
            || !parameters.Choices.Any(item => item.Key == choice.GetString()))
            throw new ArgumentException("Select one of the published task choices.");
        return JsonSerializer.Serialize(new { choice = choice.GetString() }, WorkflowDefinitionCodec.JsonOptions);
    }
}
