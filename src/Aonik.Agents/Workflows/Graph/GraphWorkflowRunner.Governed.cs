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
using Aonik.SharedKernel.Abstractions.Connectors;

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
        if ((run.Status is WorkflowRunStatuses.WaitingForConnection or WorkflowRunStatuses.NeedsAttention)
            && (run.DeadlineUtc is null || run.DeadlineUtc > clock.UtcNow)) return new(run.Status, run.Outcome, run.ErrorCode);
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
            await authority.RequireRunAsync(run, budget.Token);
            await authority.RequireEnabledAsync(budget.Token);
            // A pending approval is still subject to current domain and retained-source access.
            // Loading does not replace pinned facts or authorize a changed operation.
            _ = await authority.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, WorkflowExecutionAuthorization.Actor(run), budget.Token);
            WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == tenantId
                && version.WorkflowId == run.WorkflowId && version.Id == run.WorkflowVersionId, budget.Token);
            if (version.DefinitionHash != run.DefinitionHash) throw new WorkflowExecutionException("DefinitionHashMismatch");
            context.OriginatingAiRunId = version.AiRunId;
            var definition = WorkflowRunService.ReadDefinition(version);
            context.Definition = definition;
            if (definition.Nodes.Count > (definition.SchemaVersion == 1 ? 30 : Contracts.Models.Workflows.WorkflowControlLimits.MaximumGraphNodes)) throw new WorkflowExecutionException("GraphLimitExceeded");
            WorkflowStepExecutionService execution = scope.ServiceProvider.GetRequiredService<WorkflowStepExecutionService>();
            // A final native checkpoint has no pending work to stream. The durable End/stop receipt is authoritative after host loss.
            WorkflowExecutionFrame? recordedTerminal = run.CheckpointId is not null ? await execution.ReadRecordedTerminalAsync(budget.Token) : null;
            WorkflowExecutionFrame? recordedStop = recordedTerminal is null && run.CheckpointId is not null ? await execution.ReadRecordedStopAsync(budget.Token) : null;
            if (recordedTerminal is not null || recordedStop is not null)
            {
                status = WorkflowRunStatuses.Completed;
                outcome = recordedTerminal?.Outcome ?? recordedStop?.Outcome ?? "NeedsManualReview";
                if (recordedStop is not null || recordedTerminal?.Failed == true) error = "ExecutionStopped";
            }
            else
            {
                var workflow = GraphWorkflowBuilder.BuildGoverned(definition, execution);
                var checkpoints = CheckpointManager.CreateJson(new WorkflowCheckpointStore(database, run, lease, clock, budget.Token, definition.SchemaVersion));
                await using StreamingRun streaming = run.CheckpointId is { } checkpoint
                    ? await InProcessExecution.ResumeStreamingAsync(workflow,
                        new CheckpointInfo(run.FrameworkSessionId ?? throw new WorkflowExecutionException("CheckpointScopeMismatch"), checkpoint), checkpoints, budget.Token)
                    : await InProcessExecution.RunStreamingAsync(workflow, new WorkflowExecutionFrame(run.Id), checkpoints, cancellationToken: budget.Token);
                WorkflowExecutionFrame? terminal = null;
                WorkflowExecutionFrame? stopped = null;
                var pendingRequests = new Dictionary<string, RequestInfoEvent>(StringComparer.Ordinal);
                int maximumRequests = definition.SchemaVersion == 1 ? definition.Nodes.Count : Contracts.Models.Workflows.WorkflowControlLimits.MaximumRootInvocations;
                for (int ports = 0; ports <= maximumRequests; ports++)
                {
                    Exception? failure = null;
                    await foreach (WorkflowEvent workflowEvent in streaming.WatchStreamAsync(blockOnPendingRequest: false, budget.Token))
                    {
                        if (workflowEvent is RequestInfoEvent request)
                        {
                            pendingRequests[request.Request.RequestId] = request;
                        }
                        else if (workflowEvent is WorkflowOutputEvent { Data: WorkflowExecutionFrame frame }) terminal = frame;
                        else if (workflowEvent is ExecutorFailedEvent failed) failure = failed.Data as Exception ?? new WorkflowExecutionException("ExecutorFailed");
                        else if (workflowEvent is SubworkflowErrorEvent subworkflow) failure = subworkflow.Data as Exception ?? new WorkflowExecutionException("SubworkflowFailed");
                        else if (workflowEvent is WorkflowStoppedEvent { Data: WorkflowExecutionFrame stoppedFrame } && stoppedFrame.RunId == run.Id) stopped = stoppedFrame;
                    }
                    if (failure is not null) throw failure;
                    if (pendingRequests.Count == 0)
                    {
                        if (terminal is null) terminal = await execution.ReadRecordedTerminalAsync(budget.Token);
                        if (terminal is null && stopped is null) stopped = await execution.ReadRecordedStopAsync(budget.Token);
                        if (terminal is null && stopped is null) throw new WorkflowExecutionException("TerminalOutputMissing");
                        status = WorkflowRunStatuses.Completed;
                        outcome = terminal?.Outcome ?? stopped?.Outcome ?? "NeedsManualReview";
                        if ((stopped is not null && terminal is null) || terminal?.Failed == true) error = "ExecutionStopped";
                        break;
                    }
                    var waits = new List<(RequestInfoEvent Request, WorkflowStepWait Wait)>();
                    foreach (RequestInfoEvent pending in pendingRequests.Values)
                    {
                        if (!pending.Request.TryGetDataAs<WorkflowStepWait>(out WorkflowStepWait? wait) || wait.RunId != run.Id)
                            throw new WorkflowExecutionException("InvalidWaitRequest");
                        waits.Add((pending, wait));
                        WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == tenantId && step.Id == wait.StepRunId
                            && step.RunId == run.Id, budget.Token);
                        step.WaitCheckpointId = streaming.Checkpoints.LastOrDefault()?.CheckpointId ?? run.CheckpointId;
                        if (step.WaitCheckpointId is null) throw new WorkflowExecutionException("CheckpointMissing");
                        run.PendingStepRunId = step.Id;
                    }
                    if (waits.Count(wait => wait.Wait.Kind != "Child") > 1) throw new WorkflowExecutionException("UnexpectedParallelActionRequests");
                    await database.SaveChangesAsync(budget.Token);
                    var responses = new List<(RequestInfoEvent Request, WorkflowExecutionFrame Frame)>();
                    foreach (var (request, wait) in waits)
                        if (await execution.ReadyResponseAsync(wait, budget.Token) is { } response) responses.Add((request, response));
                    string waitingStatus = waits.Any(wait => wait.Wait.Kind == "Child") ? WorkflowRunStatuses.WaitingForChildren
                        : waits[0].Wait.Kind == "Timer" ? WorkflowRunStatuses.WaitingForTimer : WorkflowRunStatuses.WaitingForApproval;
                    if (responses.Count == 0)
                    {
                        run.Status = waitingStatus;
                        await database.SaveChangesAsync(budget.Token);
                        // Covers a decision that committed between the first read and the saved wait.
                        foreach (var (request, wait) in waits)
                            if (await execution.ReadyResponseAsync(wait, budget.Token) is { } response) responses.Add((request, response));
                        if (responses.Count == 0) { status = waitingStatus; break; }
                    }
                    run.Status = WorkflowRunStatuses.Running;
                    await database.SaveChangesAsync(budget.Token);
                    // All database reads finish before MAF resumes executor work on this context.
                    foreach (var (request, response) in responses)
                    {
                        if (response.Stopped) stopped = response;
                        pendingRequests.Remove(request.Request.RequestId);
                        await streaming.SendResponseAsync(request.Request.CreateResponse(response));
                    }
                }
                if (status == WorkflowRunStatuses.NeedsAttention) throw new WorkflowExecutionException("RequestLimitExceeded");
            }
        }
        catch (Exception exception)
        {
            Exception cause = exception.GetBaseException();
            error = cause switch
            {
                WorkflowExecutionException execution => execution.Code,
                WorkflowConflictException => "WorkflowConflict",
                SharedKernel.Abstractions.Documents.FinancialDocumentException document => document.Code,
                SharedKernel.Abstractions.Documents.DocumentTextExtractionException document => document.Code,
                ConnectorAccessException connector => connector.Code,
                ConnectorProviderException connector => connector.Code,
                PermissionDeniedException => "ExecutionAuthorityChanged",
                OperationCanceledException => "ActiveTimeLimit",
                _ => "ExecutionFailed",
            };
            status = error is "WorkflowRetired" or "RunCancelled" or "TriggerDisabled" or "RunDeadlineExceeded" or "ParentRunCancelled" ? WorkflowRunStatuses.Cancelled : WorkflowRunStatuses.NeedsAttention;
            if (cause is ConnectorAccessException || (cause is ConnectorProviderException && error is "ReauthorizationRequired" or "InsufficientScopes"))
                status = WorkflowRunStatuses.WaitingForConnection;
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
                if (run.ParentRunId.HasValue && WorkflowStepExecutionService.IsTerminal(run))
                    await scope.ServiceProvider.GetRequiredService<WorkflowContinuationWriter>().AfterChildAsync(run, CancellationToken.None);
                try { await database.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { /* A newer continuation or cancellation owns the outcome. */ }
            }
            context.Step = null;
            context.Run = null;
            context.Definition = null;
        }
        return new(run.Status, run.Outcome, run.ErrorCode);
    }
}
