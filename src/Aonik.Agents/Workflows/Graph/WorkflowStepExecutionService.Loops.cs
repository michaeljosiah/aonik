using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class WorkflowStepExecutionService
{
    public Task<WorkflowExecutionFrame> PrepareLoopAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync<WorkflowExecutionFrame>(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowLoopParameters parameters = WorkflowControlContracts.Loop(node);
            WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
            if (step.Status == "Completed") return new(Run.Id, step.SelectedRoute, ToolResult(step).Outcome, Failed: ToolResult(step).Outcome != "Completed");
            if (step.DeadlineUtc is null)
            {
                if (parameters.ItemsFromDecision is { } source)
                {
                    WorkflowStepRun decision = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == Run.TenantId
                        && step.RunId == Run.Id && step.NodeKey == source && step.InvocationPath == "root" && step.Status == "Completed", cancellationToken)
                        ?? throw new WorkflowExecutionException("CollectionSourceUnavailable");
                    DecisionEvaluation evaluated = JsonSerializer.Deserialize<DecisionEvaluation>(decision.DecisionTraceJson!, WorkflowDefinitionCodec.JsonOptions)!;
                    if (evaluated.Status != "Completed" || evaluated.Items is null || evaluated.Items.Count > WorkflowControlLimits.MaximumLoopIterations)
                        throw new WorkflowExecutionException("CollectionSourceInvalid");
                    parameters = parameters with { Items = evaluated.Items };
                }
                step.DeadlineUtc = new[] { clock.UtcNow.AddSeconds(parameters.TimeoutSeconds), Run.DeadlineUtc!.Value }.Min();
                step.ActionKey = WorkflowControlContracts.LoopActionKey; step.ActionVersion = 1; step.Status = "Looping";
                step.ArgumentsJson = JsonSerializer.Serialize(parameters, WorkflowDefinitionCodec.JsonOptions);
                step.ArgumentsHash = Hash(JsonSerializer.Deserialize<JsonElement>(step.ArgumentsJson));
                step.ResultJson = JsonSerializer.Serialize(new WorkflowLoopState(0, []), WorkflowDefinitionCodec.JsonOptions);
                database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(step.Id, clock.UtcNow, Run.TenantId, Run.Id, Run.Continuation,
                    DateTime.SpecifyKind(step.DeadlineUtc.Value, DateTimeKind.Utc)));
                await database.SaveChangesAsync(cancellationToken);
            }
            else parameters = StoredLoopParameters(step);
            WorkflowLoopState state = ReadLoopState(step);
            string? route = state.Index >= parameters.Items.Count ? parameters.CompletedRoute
                : clock.UtcNow >= step.DeadlineUtc || state.Index >= parameters.MaxIterations ? parameters.ExhaustedRoute : null;
            return new(Run.Id, route);
        });

    public Task<WorkflowStepWait> PrepareIterationAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowStepRun loop = await StepAsync(node.Key, cancellationToken);
            WorkflowLoopParameters parameters = StoredLoopParameters(loop);
            WorkflowLoopState state = ReadLoopState(loop);
            if (state.Index >= Math.Min(parameters.Items.Count, parameters.MaxIterations) || loop.DeadlineUtc <= clock.UtcNow)
                throw new WorkflowExecutionException("IterationNotAdmitted");
            WorkflowLoopItem item = parameters.Items[state.Index];
            return await PrepareChildCallAsync(new(node.Key, $"root/{item.Key}", parameters.Body,
                new(Literal: JsonSerializer.SerializeToElement(item.InvoiceId)), "iteration-completed", "iteration-error", loop.DeadlineUtc, loop.Id, item.Key), frame, cancellationToken);
        });

    public Task<WorkflowExecutionFrame> CompleteIterationAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync<WorkflowExecutionFrame>(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            if (frame.ResumedStepRunId is not { } stepId) throw new WorkflowExecutionException("IterationIdentityMissing");
            WorkflowStepRun call = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
                && step.Id == stepId && step.NodeKey == node.Key, cancellationToken);
            WorkflowStepRun loop = await StepAsync(node.Key, cancellationToken);
            WorkflowChildEvidence evidence = JsonSerializer.Deserialize<WorkflowChildEvidence>(call.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions)!;
            if (evidence.OwnerStepRunId != loop.Id || evidence.ItemKey is null) throw new WorkflowExecutionException("IterationIdentityMismatch");
            if (loop.Status == "Completed") return new(Run.Id, loop.SelectedRoute, ToolResult(loop).Outcome, Failed: ToolResult(loop).Outcome != "Completed");
            WorkflowLoopParameters parameters = StoredLoopParameters(loop);
            WorkflowLoopState state = ReadLoopState(loop);
            WorkflowExecutionFrame completed = await CompleteChildCallAsync(call, cancellationToken);
            bool success = completed.Route == "iteration-completed";
            WorkflowLoopReceipt? previous = state.Iterations.SingleOrDefault(receipt => receipt.StepRunId == call.Id);
            if (previous is null)
            {
                if (state.Index >= parameters.Items.Count || parameters.Items[state.Index].Key != evidence.ItemKey)
                    throw new WorkflowExecutionException("IterationSequenceMismatch");
                WorkflowRun child = await ChildForStepAsync(call, cancellationToken);
                state = new(state.Index + 1, [.. state.Iterations, new(call.Id, evidence.ItemKey, child.Id, success, completed.Outcome ?? "NeedsManualReview")]);
                loop.ResultJson = JsonSerializer.Serialize(state, WorkflowDefinitionCodec.JsonOptions);
                await database.SaveChangesAsync(cancellationToken);
            }
            string? route = clock.UtcNow >= loop.DeadlineUtc ? parameters.ExhaustedRoute : success ? null : parameters.ErrorRoute;
            return new(Run.Id, route);
        });

    public Task<WorkflowExecutionFrame> CompleteLoopAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync<WorkflowExecutionFrame>(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
            if (step.Status == "Completed") return new(Run.Id, step.SelectedRoute, ToolResult(step).Outcome, Failed: ToolResult(step).Outcome != "Completed");
            WorkflowLoopParameters parameters = StoredLoopParameters(step);
            if (!WorkflowControlContracts.Routes(node).Contains(frame.Route)) throw new WorkflowExecutionException("LoopTerminalRouteMissing");
            WorkflowLoopState state = ReadLoopState(step);
            string outcome = frame.Route == parameters.CompletedRoute ? "Completed" : frame.Route == parameters.ExhaustedRoute ? "Exhausted" : "NeedsManualReview";
            step.Status = "Completed"; step.SelectedRoute = frame.Route; step.CompletedAt = clock.UtcNow;
            step.ResultJson = JsonSerializer.Serialize(new WorkflowActionResult(outcome, JsonSerializer.SerializeToElement(new
                { iterations = state.Iterations, processedCount = state.Index, totalCount = parameters.Items.Count }, WorkflowDefinitionCodec.JsonOptions)), WorkflowDefinitionCodec.JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            return new(Run.Id, frame.Route, outcome, Failed: outcome != "Completed");
        });

    private static WorkflowLoopState ReadLoopState(WorkflowStepRun step)
        => JsonSerializer.Deserialize<WorkflowLoopState>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions) ?? throw new WorkflowExecutionException("LoopStateMissing");
    private static WorkflowLoopParameters StoredLoopParameters(WorkflowStepRun step)
        => JsonSerializer.Deserialize<WorkflowLoopParameters>(step.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions) ?? throw new WorkflowExecutionException("LoopArgumentsMissing");
}

internal sealed record WorkflowLoopState(int Index, IReadOnlyList<WorkflowLoopReceipt> Iterations);
internal sealed record WorkflowLoopReceipt(Guid StepRunId, string Key, Guid ChildRunId, bool Success, string Outcome);
