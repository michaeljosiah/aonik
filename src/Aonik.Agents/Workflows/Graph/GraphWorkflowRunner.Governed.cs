using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class GraphWorkflowRunner
{
    public async Task<GraphWorkflowContinuation> ContinueAsync(Guid runId, int continuation, CancellationToken cancellationToken = default)
    {
        Guid tenantId = _serviceProvider.GetRequiredService<ITenantProvider>().GetCurrentTenantId();
        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        ChatThreadManager.SeedBackgroundContext(scope.ServiceProvider, tenantId, null, "workflow-continuation");
        AgentsDbContext database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();
        WorkflowRun? run = await database.WorkflowRuns.SingleOrDefaultAsync(run => run.TenantId == tenantId && run.Id == runId
            && run.Mode == "Live", cancellationToken);
        if (run is null || run.Continuation != continuation) return new("Skipped");
        if (run.Status is WorkflowRunStatuses.Completed or WorkflowRunStatuses.Failed) return new(run.Status, run.Outcome, run.ErrorCode);
        bool cancelled = run.Status == WorkflowRunStatuses.Cancelled;
        if (cancelled && !await database.WorkflowStepRuns.AsNoTracking().AnyAsync(step => step.TenantId == tenantId && step.RunId == run.Id
            && step.Id == run.PendingStepRunId && step.Status == "Executing", cancellationToken)) return new(run.Status, run.Outcome, run.ErrorCode);
        if (run.LeaseExpiresAt > clock.UtcNow) return new("Busy");
        Guid lease = Guid.NewGuid();
        run.LeaseId = lease;
        run.LeaseExpiresAt = clock.UtcNow.AddMinutes(2);
        if (!cancelled) run.Status = WorkflowRunStatuses.Running;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new("Busy"); }

        WorkflowInvocationContext context = scope.ServiceProvider.GetRequiredService<WorkflowInvocationContext>();
        context.Run = run;
        context.LeaseId = lease;
        string status = WorkflowRunStatuses.NeedsAttention;
        string? outcome = null;
        string? error = null;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            if (cancelled)
            {
                await scope.ServiceProvider.GetRequiredService<WorkflowStepExecutionService>().ReconcileCancelledAsync(budget.Token);
                return new(run.Status, run.Outcome, run.ErrorCode);
            }
            WorkflowExecutionAuthorization authority = scope.ServiceProvider.GetRequiredService<WorkflowExecutionAuthorization>();
            await authority.RequireActorAsync(WorkflowExecutionAuthorization.Actor(run), budget.Token);
            await authority.RequireEnabledAsync(budget.Token);
            WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == tenantId
                && version.WorkflowId == run.WorkflowId && version.Id == run.WorkflowVersionId, budget.Token);
            if (version.DefinitionHash != run.DefinitionHash) throw new WorkflowExecutionException("DefinitionHashMismatch");
            context.OriginatingAiRunId = version.AiRunId;
            var definition = WorkflowRunService.ReadDefinition(version);
            if (definition.Nodes.Count > 30) throw new WorkflowExecutionException("GraphLimitExceeded");
            WorkflowStepExecutionService execution = scope.ServiceProvider.GetRequiredService<WorkflowStepExecutionService>();
            var workflow = GraphWorkflowBuilder.BuildGoverned(definition, execution);
            var checkpoints = CheckpointManager.CreateJson(new WorkflowCheckpointStore(database, run, lease, clock, budget.Token));
            await using StreamingRun streaming = run.CheckpointId is { } checkpoint
                ? await InProcessExecution.ResumeStreamingAsync(workflow,
                    new CheckpointInfo(run.FrameworkSessionId ?? throw new WorkflowExecutionException("CheckpointScopeMismatch"), checkpoint), checkpoints, budget.Token)
                : await InProcessExecution.RunStreamingAsync(workflow, new WorkflowExecutionFrame(run.Id), checkpoints, cancellationToken: budget.Token);
            WorkflowExecutionFrame? terminal = null;
            WorkflowExecutionFrame? stopped = null;
            for (int ports = 0; ports <= definition.Nodes.Count; ports++)
            {
                RequestInfoEvent? pending = null;
                Exception? failure = null;
                await foreach (WorkflowEvent workflowEvent in streaming.WatchStreamAsync(blockOnPendingRequest: false, budget.Token))
                {
                    if (workflowEvent is RequestInfoEvent request)
                    {
                        if (pending is not null) throw new WorkflowExecutionException("UnexpectedParallelRequests");
                        pending = request;
                    }
                    else if (workflowEvent is WorkflowOutputEvent { Data: WorkflowExecutionFrame frame }) terminal = frame;
                    else if (workflowEvent is ExecutorFailedEvent failed) failure = failed.Data as Exception ?? new WorkflowExecutionException("ExecutorFailed");
                }
                if (failure is not null) throw failure;
                if (pending is null)
                {
                    status = WorkflowRunStatuses.Completed;
                    outcome = terminal?.Outcome ?? stopped?.Outcome ?? "NeedsManualReview";
                    break;
                }
                if (!pending.Request.TryGetDataAs<WorkflowStepWait>(out WorkflowStepWait? wait) || wait.RunId != run.Id)
                    throw new WorkflowExecutionException("InvalidWaitRequest");
                WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == tenantId && step.Id == wait.StepRunId
                    && step.RunId == run.Id, budget.Token);
                step.WaitCheckpointId = streaming.Checkpoints.LastOrDefault()?.CheckpointId ?? run.CheckpointId;
                if (step.WaitCheckpointId is null) throw new WorkflowExecutionException("CheckpointMissing");
                run.PendingStepRunId = step.Id;
                await database.SaveChangesAsync(budget.Token);
                WorkflowExecutionFrame? response = await execution.ReadyResponseAsync(wait, budget.Token);
                if (response is null)
                {
                    run.Status = WorkflowRunStatuses.WaitingForApproval;
                    await database.SaveChangesAsync(budget.Token);
                    // Covers a decision that committed between the first read and the saved wait.
                    response = await execution.ReadyResponseAsync(wait, budget.Token);
                    if (response is null) { status = WorkflowRunStatuses.WaitingForApproval; break; }
                }
                run.Status = WorkflowRunStatuses.Running;
                await database.SaveChangesAsync(budget.Token);
                if (response.Stopped) stopped = response;
                await streaming.SendResponseAsync(pending.Request.CreateResponse(response));
            }
        }
        catch (Exception exception)
        {
            Exception cause = exception.GetBaseException();
            error = cause switch
            {
                WorkflowExecutionException execution => execution.Code,
                WorkflowConflictException => "WorkflowConflict",
                PermissionDeniedException => "ExecutionAuthorityChanged",
                OperationCanceledException => "ActiveTimeLimit",
                _ => "ExecutionFailed",
            };
            status = error is "WorkflowRetired" or "RunCancelled" ? WorkflowRunStatuses.Cancelled : WorkflowRunStatuses.NeedsAttention;
            outcome = error == "SubjectChanged" ? "NeedsManualReview" : null;
            _logger.LogWarning("Workflow {RunId} stopped with {ErrorCode}", run.Id, error);
            if (run.PendingStepRunId is { } stepId)
            {
                WorkflowStepRun? step = await database.WorkflowStepRuns.SingleOrDefaultAsync(step => step.TenantId == tenantId && step.Id == stepId, CancellationToken.None);
                if (step?.WaitCheckpointId is { } safeCheckpoint) run.CheckpointId = safeCheckpoint;
            }
        }
        finally
        {
            string? checkpoint = run.CheckpointId;
            await database.Entry(run).ReloadAsync(CancellationToken.None);
            if (run.LeaseId == lease)
            {
                if (run.Status != WorkflowRunStatuses.Cancelled)
                {
                    run.Status = status;
                    run.Outcome = outcome;
                    run.ErrorCode = error;
                    run.CheckpointId = checkpoint;
                    if (status is WorkflowRunStatuses.Completed or WorkflowRunStatuses.Cancelled) run.CompletedAt = clock.UtcNow;
                    if (status == WorkflowRunStatuses.Cancelled)
                        await WorkflowRunCancellation.StageAsync(database, run, error ?? "RunCancelled", clock.UtcNow, CancellationToken.None);
                }
                run.LeaseId = null;
                run.LeaseExpiresAt = null;
                run.DurationMs = (int)Math.Clamp((clock.UtcNow - run.StartedAt).TotalMilliseconds, 0, int.MaxValue);
                try { await database.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { /* A newer continuation or cancellation owns the outcome. */ }
            }
            context.Step = null;
            context.Run = null;
        }
        return new(run.Status, run.Outcome, run.ErrorCode);
    }
}
