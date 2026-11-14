using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class WorkflowStepExecutionService
{
    // Only parent hand-off/audit operations share this context. Child I/O uses its own existing runner scope.
    private readonly SemaphoreSlim _controlWrites = new(1, 1);

    internal async Task<T> SerializeControlAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _controlWrites.WaitAsync(cancellationToken);
        try { return await operation(); }
        finally { _controlWrites.Release(); }
    }

    public Task<WorkflowStepWait> PrepareChildAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        WorkflowChildParameters parameters = WorkflowControlContracts.Child(node);
        return SerializeControlAsync(() => PrepareChildCallAsync(new(node.Key, "root", parameters.Workflow, parameters.InvoiceId,
            parameters.CompletedRoute, parameters.ErrorRoute), frame, cancellationToken));
    }

    private async Task<WorkflowStepWait> PrepareChildCallAsync(WorkflowChildCall call, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        if (invocationContext.Definition?.SchemaVersion != 2) throw new WorkflowExecutionException("ControlSchemaRequired");
        WorkflowStepRun step = await StepAsync(call.NodeKey, cancellationToken, call.InvocationPath);
        WorkflowRun? existing = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(child => child.TenantId == Run.TenantId
            && child.ParentRunId == Run.Id && child.ParentStepRunId == step.Id, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkflowVersionId != call.Pin.VersionId || existing.DefinitionHash != call.Pin.DefinitionHash)
                throw new WorkflowExecutionException("ChildPinChanged");
            return new(Run.Id, step.Id, "Child");
        }
        WorkflowFactsSnapshot facts = await UnchangedFactsAsync(cancellationToken);
        var parameters = new WorkflowToolParameters(WorkflowControlContracts.ChildActionKey, 1,
            new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = call.InvoiceId });
        JsonElement input = WorkflowInputBinder.Bind(parameters, facts, await OutputsAsync(cancellationToken));
        if (!input.GetProperty("invoiceId").TryGetGuid(out Guid invoiceId) || invoiceId == Guid.Empty) throw new WorkflowExecutionException("ChildInputInvalid");
        DateTime? deadlineUtc = call.DeadlineUtc is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null;
        JsonElement arguments = JsonSerializer.SerializeToElement(new WorkflowChildEvidence(call.Pin, invoiceId, call.CompletedRoute, call.ErrorRoute,
            call.OwnerStepRunId, call.ItemKey, deadlineUtc), WorkflowDefinitionCodec.JsonOptions);
        if (step.ArgumentsHash is { } previous && previous != Hash(arguments)) throw new WorkflowExecutionException("ChildInputChanged");
        step.ActionKey = WorkflowControlContracts.ChildActionKey; step.ActionVersion = 1;
        step.RequiredPermission = "Workflow.Run"; step.ArgumentsJson = arguments.GetRawText(); step.ArgumentsHash = Hash(arguments);
        step.FactsJson = JsonSerializer.Serialize(facts, WorkflowDefinitionCodec.JsonOptions); step.FactsHash = WorkflowFactsHash.Compute(facts);
        step.ExpectedSubjectVersion = facts.SubjectVersion; step.Status = "WaitingForChild";
        Run.PendingStepRunId = step.Id;
        await database.SaveChangesAsync(cancellationToken);
        _ = await admissions.AdmitChildAsync(Run, step, call.Pin, invoiceId, call.DeadlineUtc, cancellationToken);
        return new(Run.Id, step.Id, "Child");
    }

    private async Task<WorkflowExecutionFrame?> ReadyChildResponseAsync(WorkflowStepWait wait, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(wait.RunId, cancellationToken, allowWaiting: true);
        WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
            && step.Id == wait.StepRunId, cancellationToken);
        await database.Entry(step).ReloadAsync(cancellationToken);
        if (step.Status == "Completed") return ChildFrame(step);
        if (step.Status == "Cancelled") throw new WorkflowExecutionException("RunCancelled");
        WorkflowRun child = await ChildForStepAsync(step, cancellationToken);
        WorkflowChildEvidence evidence = JsonSerializer.Deserialize<WorkflowChildEvidence>(step.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions)!;
        if (evidence.DeadlineUtc is { } deadline && deadline <= clock.UtcNow && !IsTerminal(child))
            child = await CancelChildAsync(child.Id, "ControlDeadlineExceeded", cancellationToken);
        if (child.Status == WorkflowRunStatuses.Cancelled && await HasUnsettledEffectAsync(child.Id, cancellationToken)) return null;
        return IsTerminal(child) ? new(Run.Id, ResumedStepRunId: step.Id) : null;
    }

    public Task<WorkflowExecutionFrame> CompleteChildAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
            if (frame.ResumedStepRunId is { } resumed && resumed != step.Id) throw new WorkflowExecutionException("ChildWaitIdentityMismatch");
            return await CompleteChildCallAsync(step, cancellationToken);
        });

    private async Task<WorkflowExecutionFrame> CompleteChildCallAsync(WorkflowStepRun step, CancellationToken cancellationToken)
    {
        if (step.Status == "Completed") return ChildFrame(step);
        WorkflowRun child = await ChildForStepAsync(step, cancellationToken);
        if (!IsTerminal(child)) throw new WorkflowExecutionException("ChildStillPending");
        WorkflowChildEvidence evidence = JsonSerializer.Deserialize<WorkflowChildEvidence>(step.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions)!;
        if (child.WorkflowVersionId != evidence.Pin.VersionId || child.DefinitionHash != evidence.Pin.DefinitionHash || child.SubjectId != evidence.InvoiceId
            || child.ActorType != Run.ActorType || child.ActorId != Run.ActorId || child.ExecutionRootRunId != (Run.ExecutionRootRunId ?? Run.Id))
            throw new WorkflowExecutionException("ChildReceiptMismatch");
        WorkflowFactsSnapshot current = await authorization.LoadFactsAsync(Run.SubjectType!, Run.SubjectId!.Value, WorkflowExecutionAuthorization.Actor(Run), cancellationToken);
        string currentHash = WorkflowFactsHash.Compute(current);
        if (currentHash != Run.FactsHash && !(child.SubjectId == Run.SubjectId && child.SubjectType == Run.SubjectType && child.FactsHash == currentHash && ChildSucceeded(child)))
            throw new WorkflowExecutionException("SubjectChanged");
        Run.FactsJson = JsonSerializer.Serialize(current, WorkflowDefinitionCodec.JsonOptions); Run.FactsHash = currentHash;
        WorkflowActionResult receipt = new(child.Outcome ?? (child.Status == WorkflowRunStatuses.Cancelled ? "Cancelled" : "NeedsManualReview"),
            JsonSerializer.SerializeToElement(new { childRunId = child.Id, outcome = child.Outcome ?? child.Status }, WorkflowDefinitionCodec.JsonOptions));
        step.Status = "Completed"; step.CompletedAt = clock.UtcNow; step.SelectedRoute = ChildSucceeded(child) ? evidence.CompletedRoute : evidence.ErrorRoute;
        step.ResultJson = JsonSerializer.Serialize(receipt, WorkflowDefinitionCodec.JsonOptions);
        if (Run.PendingStepRunId == step.Id) Run.PendingStepRunId = null;
        await database.SaveChangesAsync(cancellationToken);
        return new(Run.Id, step.SelectedRoute, receipt.Outcome, ResumedStepRunId: step.Id, Failed: !ChildSucceeded(child));
    }

    private WorkflowExecutionFrame ChildFrame(WorkflowStepRun step)
    {
        WorkflowChildEvidence evidence = JsonSerializer.Deserialize<WorkflowChildEvidence>(step.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions)!;
        return new(Run.Id, step.SelectedRoute, ToolResult(step).Outcome, ResumedStepRunId: step.Id, Failed: step.SelectedRoute != evidence.CompletedRoute);
    }

    private async Task<WorkflowRun> CancelChildAsync(Guid childId, string reason, CancellationToken cancellationToken)
    {
        WorkflowRun child = await database.WorkflowRuns.SingleAsync(child => child.TenantId == Run.TenantId && child.ParentRunId == Run.Id && child.Id == childId, cancellationToken);
        await database.Entry(child).ReloadAsync(cancellationToken);
        if (IsTerminal(child)) return child;
        child.Status = WorkflowRunStatuses.Cancelled; child.Outcome = "Cancelled"; child.ErrorCode = reason; child.CompletedAt = clock.UtcNow;
        await WorkflowRunCancellation.StageAsync(database, child, reason, clock.UtcNow, cancellationToken);
        if (await database.WorkflowStepRuns.AnyAsync(step => step.RunId == child.Id && step.TenantId == Run.TenantId && step.Status == "Executing", cancellationToken))
            admissions.Queue(child, child.LeaseExpiresAt is { } lease && lease > clock.UtcNow ? lease : clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        return child;
    }

    private async Task<bool> HasUnsettledEffectAsync(Guid childId, CancellationToken cancellationToken)
    {
        List<WorkflowStepRun> pending = await database.WorkflowStepRuns.AsNoTracking().Where(step => step.TenantId == Run.TenantId && step.RunId == childId
            && (step.Status == "Executing" || step.Status == "NeedsAttention") && step.ActionKey != null && step.ResultJson == null).ToListAsync(cancellationToken);
        return pending.Any(step => actions.Resolve(step.ActionKey!, step.ActionVersion!.Value).Descriptor.Approval is not null);
    }

    private Task<WorkflowRun> ChildForStepAsync(WorkflowStepRun step, CancellationToken cancellationToken)
        => database.WorkflowRuns.AsNoTracking().SingleAsync(child => child.TenantId == Run.TenantId && child.ParentRunId == Run.Id
            && child.ParentStepRunId == step.Id, cancellationToken);
    internal static bool IsTerminal(WorkflowRun run) => run.Status is WorkflowRunStatuses.Completed or WorkflowRunStatuses.Cancelled or WorkflowRunStatuses.Failed;
    internal static bool ChildSucceeded(WorkflowRun run) => run.Status == WorkflowRunStatuses.Completed
        && run.Outcome is not (null or "NeedsManualReview" or "Blocked" or "NeedsAttention" or "Exhausted") && run.ErrorCode is null;
}

internal sealed record WorkflowChildCall(string NodeKey, string InvocationPath, WorkflowVersionPin Pin, WorkflowInputBinding InvoiceId,
    string CompletedRoute, string ErrorRoute, DateTime? DeadlineUtc = null, Guid? OwnerStepRunId = null, string? ItemKey = null);
internal sealed record WorkflowChildEvidence(WorkflowVersionPin Pin, Guid InvoiceId, string CompletedRoute, string ErrorRoute,
    Guid? OwnerStepRunId = null, string? ItemKey = null, DateTime? DeadlineUtc = null);
