using System.Text.Json;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowRunService(
    AgentsDbContext database, ITenantProvider tenantProvider, WorkflowAuthorization authorization,
    WorkflowExecutionAuthorization executionAuthorization, IClock clock, WorkflowActionRegistry? actions = null,
    WorkflowReviewerAuthorization? reviewers = null) : IWorkflowRunService
{
    public async Task<WorkflowRunDetail> StartAsync(string slug, StartWorkflowRunRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Run", cancellationToken);
        var actor = new WorkflowActor("User", userId, userId);
        await executionAuthorization.RequireActorAsync(actor, cancellationToken);
        await executionAuthorization.RequireEnabledAsync(cancellationToken);
        ValidateRequest(request);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        string inputHash = InputHash(slug, request);
        WorkflowRun? existing = await FindReplayAsync(tenantId, userId, request.IdempotencyKey, cancellationToken);
        if (existing is not null) return await ReplayAsync(existing, inputHash, actor, cancellationToken);
        (Workflow workflow, WorkflowVersion version, WorkflowFactsSnapshot facts) = await PrepareAdmissionAsync(slug, request, actor, cancellationToken);
        var run = new WorkflowRun
        {
            TenantId = tenantId, WorkflowId = workflow.Id, WorkflowVersionId = version.Id, DefinitionHash = version.DefinitionHash,
            Mode = "Live", ActorType = actor.Type, ActorId = actor.Id, InitiatingUserId = userId,
            SubjectType = request.SubjectType, SubjectId = request.SubjectId, IdempotencyKey = request.IdempotencyKey,
            InputHash = inputHash, FactsJson = JsonSerializer.Serialize(facts, WorkflowDefinitionCodec.JsonOptions), FactsHash = WorkflowFactsHash.Compute(facts),
            Status = WorkflowRunStatuses.Queued, StartedAt = clock.UtcNow, StartedBy = $"manual:{userId:D}",
        };
        database.WorkflowRuns.Add(run);
        // Updating this tracked root also detects a concurrent retirement/publication during admission.
        workflow.TriggerCount = checked(workflow.TriggerCount + 1);
        Guid eventId = Queue(run, clock.UtcNow);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            database.Entry(run).State = EntityState.Detached;
            foreach (var entry in database.ChangeTracker.Entries<OutboxMessage>().Where(entry => entry.Entity.EventId == eventId))
                entry.State = EntityState.Detached;
            await database.Entry(workflow).ReloadAsync(cancellationToken);
            existing = await FindReplayAsync(tenantId, userId, request.IdempotencyKey, cancellationToken);
            if (existing is null) throw;
            return await ReplayAsync(existing, inputHash, actor, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The workflow changed during admission. Retry the same admission key."); }
        return await DetailAsync(run, cancellationToken);
    }

    internal async Task<(StartWorkflowRunRequest Request, ToolApprovalPreview Preview)> PrepareStartAsync(
        string slug, StartWorkflowRunRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Run", cancellationToken);
        var actor = new WorkflowActor("User", userId, userId);
        await executionAuthorization.RequireActorAsync(actor, cancellationToken);
        await executionAuthorization.RequireEnabledAsync(cancellationToken);
        ValidateRequest(request);
        (_, WorkflowVersion version, WorkflowFactsSnapshot facts) = await PrepareAdmissionAsync(slug, request, actor, cancellationToken);
        return (request with { ExpectedFactsHash = WorkflowFactsHash.Compute(facts) },
            new("Start a published workflow", null, JsonSerializer.SerializeToElement(new
            {
                slug, workflowVersionId = version.Id, version.DefinitionHash, request.SubjectType, request.SubjectId,
                facts = facts.Facts, factsHash = WorkflowFactsHash.Compute(facts), request.IdempotencyKey,
            }, WorkflowDefinitionCodec.JsonOptions)));
    }

    private async Task<(Workflow, WorkflowVersion, WorkflowFactsSnapshot)> PrepareAdmissionAsync(
        string slug, StartWorkflowRunRequest request, WorkflowActor actor, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        Workflow workflow = await database.Workflows.SingleOrDefaultAsync(workflow => workflow.TenantId == tenantId && workflow.Slug == slug, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow not found.");
        if (workflow.PublishedVersionId != request.WorkflowVersionId || workflow.RetiredAt.HasValue || workflow.State != WorkflowStates.Active)
            throw new WorkflowConflictException("Only the current published, non-retired workflow version can admit a new run.");
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == tenantId
            && version.WorkflowId == workflow.Id && version.Id == request.WorkflowVersionId, cancellationToken);
        ReadDefinition(version);
        WorkflowFactsSnapshot facts = await executionAuthorization.LoadFactsAsync(request.SubjectType, request.SubjectId, actor, cancellationToken);
        if (request.ExpectedFactsHash is { } expected && expected != WorkflowFactsHash.Compute(facts))
            throw new WorkflowConflictException("The subject changed after the workflow start was prepared. Review it again.");
        return (workflow, version, facts);
    }

    private static void ValidateRequest(StartWorkflowRunRequest request)
    {
        if (request is null || request.WorkflowVersionId == Guid.Empty || request.SubjectId == Guid.Empty
            || request.SubjectType != "Invoice" || !WorkflowDefinitionCodec.IsKey(request.IdempotencyKey))
            throw new ArgumentException("Provide a published version, Invoice subject and stable idempotency key.");
    }

    public async Task<WorkflowRunDetail?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Read", cancellationToken);
        WorkflowRun? run = await FindAsync(runId, cancellationToken);
        if (run is null) return null;
        if (run.SubjectId is { } subjectId && run.SubjectType is { } subjectType)
            await executionAuthorization.LoadFactsAsync(subjectType, subjectId, new("User", userId, userId), cancellationToken);
        return await DetailAsync(run, cancellationToken);
    }

    public async Task<WorkflowRunDetail> ResumeAsync(Guid runId, WorkflowRunCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Run", cancellationToken);
        await executionAuthorization.RequireActorAsync(new("User", userId, userId), cancellationToken);
        await executionAuthorization.RequireEnabledAsync(cancellationToken);
        WorkflowRun run = await FindAsync(runId, cancellationToken) ?? throw new KeyNotFoundException("Workflow run not found.");
        CheckVersion(run, command.ExpectedVersion);
        await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        if (run.Status is not (WorkflowRunStatuses.WaitingForConnection or WorkflowRunStatuses.NeedsAttention))
            throw new WorkflowConflictException("Only a run held for connection access or operator attention can be explicitly resumed.");
        await executionAuthorization.RequireActorAsync(WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        WorkflowFactsSnapshot current = await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value,
            WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != run.FactsHash && !await HasAppliedPendingOperationAsync(run, cancellationToken))
            throw new WorkflowConflictException("The workflow subject changed. Review a new run instead of replacing its approved facts.");
        run.Status = WorkflowRunStatuses.Queued;
        run.ErrorCode = null;
        run.Continuation++;
        Queue(run, clock.UtcNow);
        await SaveAsync(cancellationToken);
        return await DetailAsync(run, cancellationToken);
    }

    private async Task<bool> HasAppliedPendingOperationAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        if (actions is null || run.PendingStepRunId is not { } pending) return false;
        WorkflowStepRun? step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == run.TenantId
            && step.RunId == run.Id && step.Id == pending && step.Status == "Executing", cancellationToken);
        if (step is null || step.ActionKey is null || step.ActionVersion is null || step.ArgumentsJson is null) return false;
        IWorkflowActionHandler handler = actions.Resolve(step.ActionKey, step.ActionVersion.Value);
        await executionAuthorization.RequireModuleAsync(handler.GetType(), cancellationToken);
        var invocation = new WorkflowActionInvocation(run.Id, step.Id, step.OperationId, WorkflowExecutionAuthorization.Actor(run),
            run.SubjectType!, run.SubjectId!.Value, step.ExpectedSubjectVersion!, step.FactsHash!,
            JsonSerializer.Deserialize<JsonElement>(step.ArgumentsJson), step.ApprovalRequestId);
        return (await handler.ReconcileAsync(invocation, cancellationToken)).Result is not null;
    }

    public async Task<WorkflowRunDetail> CancelAsync(Guid runId, WorkflowRunCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Run", cancellationToken);
        await executionAuthorization.RequireActorAsync(new("User", userId, userId), cancellationToken);
        WorkflowRun run = await FindAsync(runId, cancellationToken) ?? throw new KeyNotFoundException("Workflow run not found.");
        CheckVersion(run, command.ExpectedVersion);
        await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        if (run.Status is WorkflowRunStatuses.Completed or WorkflowRunStatuses.Cancelled or WorkflowRunStatuses.Failed)
            throw new WorkflowConflictException("The workflow run is already terminal.");
        run.Status = WorkflowRunStatuses.Cancelled;
        run.Outcome = "Cancelled";
        run.CompletedAt = clock.UtcNow;
        run.Continuation++;
        await WorkflowRunCancellation.StageAsync(database, run, "RunCancelled", clock.UtcNow, cancellationToken);
        // A late continuation can reconcile an already dispatched operation, but cannot start another action.
        Queue(run, run.LeaseExpiresAt is { } expiry && expiry > clock.UtcNow ? expiry : clock.UtcNow);
        await SaveAsync(cancellationToken);
        return await DetailAsync(run, cancellationToken);
    }

    internal Guid Queue(WorkflowRun run, DateTime notBeforeUtc)
    {
        Guid eventId = Guid.NewGuid();
        database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(eventId, clock.UtcNow, run.TenantId, run.Id, run.Continuation, notBeforeUtc));
        return eventId;
    }

    internal static WorkflowDefinition ReadDefinition(WorkflowVersion version)
    {
        if (version.PublishedAt is null || version.SchemaVersion != 1 || version.DefinitionJson is not { } json
            || WorkflowDefinitionCodec.Hash(json) != version.DefinitionHash)
            throw new WorkflowConflictException("This version is not an intact executable snapshot.");
        WorkflowDefinition definition = JsonSerializer.Deserialize<WorkflowDefinition>(json, WorkflowDefinitionCodec.JsonOptions)
            ?? throw new WorkflowConflictException("Workflow snapshot is invalid.");
        if (WorkflowDefinitionValidator.Validate(definition, forPublication: true).Any(diagnostic => diagnostic.Severity == "Error"))
            throw new WorkflowConflictException("The workflow version uses unavailable capabilities or invalid contracts.");
        return definition;
    }

    private Task<WorkflowRun?> FindAsync(Guid runId, CancellationToken cancellationToken) => database.WorkflowRuns
        .SingleOrDefaultAsync(run => run.TenantId == tenantProvider.GetCurrentTenantId() && run.Id == runId && run.Mode == "Live", cancellationToken);

    private Task<WorkflowRun?> FindReplayAsync(Guid tenantId, Guid actorId, string key, CancellationToken cancellationToken) => database.WorkflowRuns.AsNoTracking()
        .SingleOrDefaultAsync(run => run.TenantId == tenantId && run.ActorType == "User" && run.ActorId == actorId && run.IdempotencyKey == key, cancellationToken);

    private async Task<WorkflowRunDetail> ReplayAsync(WorkflowRun run, string inputHash, WorkflowActor actor, CancellationToken cancellationToken)
    {
        if (run.InputHash != inputHash) throw new WorkflowConflictException("The admission key is already bound to different input.");
        await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, actor, cancellationToken);
        return await DetailAsync(run, cancellationToken);
    }

    internal async Task<WorkflowRunDetail> DetailAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        List<WorkflowStepRun> steps = await database.WorkflowStepRuns.AsNoTracking().Where(step => step.TenantId == run.TenantId && step.RunId == run.Id)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, int> order = WorkflowService.ParseGuidArray(run.SequenceJson).Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
        steps = steps.OrderBy(step => order.GetValueOrDefault(step.Id, int.MaxValue)).ThenBy(step => step.StartedAt).ThenBy(step => step.Id).ToList();
        Guid userId = await authorization.RequireAsync("Workflow.Read", cancellationToken);
        Guid[] stepIds = steps.Select(step => step.Id).ToArray();
        Dictionary<Guid, ToolApprovalRequest> requests = await database.ToolApprovalRequests.AsNoTracking()
            .Where(request => request.TenantId == run.TenantId && request.WorkflowStepRunId.HasValue && stepIds.Contains(request.WorkflowStepRunId.Value))
            .ToDictionaryAsync(request => request.WorkflowStepRunId!.Value, cancellationToken);
        var details = new List<WorkflowStepDetail>();
        foreach (WorkflowStepRun step in steps)
        {
            WorkflowStepApproval? approval = null;
            if (requests.TryGetValue(step.Id, out ToolApprovalRequest? request))
            {
                bool canDecide = false;
                if (reviewers is not null && request.Status == ToolApprovalRequestStatus.Pending)
                {
                    try { await reviewers.RequireAsync(request, userId, cancellationToken); canDecide = true; }
                    catch (Exception exception) when (exception is PermissionDeniedException or WorkflowConflictException) { }
                }
                approval = new(request.Id, request.RiskTier, request.ActionKind, request.Status.ToString(), request.ExpiresAt, canDecide,
                    request.PreviewJson is { } preview ? JsonSerializer.Deserialize<ToolApprovalPreview>(preview, WorkflowDefinitionCodec.JsonOptions) : null);
            }
            details.Add(new(step.Id, step.NodeKey, step.InvocationPath, step.Status, step.ActionKey, step.ActionVersion,
                step.SelectedRoute, ReadJson(step.DecisionTraceJson), ReadJson(step.ResultJson), step.ApprovalRequestId, step.ProposalId, step.AiRunId, step.ErrorCode, approval));
        }
        return new(run.Id, run.WorkflowId, run.WorkflowVersionId, run.DefinitionHash, run.Status, run.Outcome, run.ErrorCode,
            run.ActorType, run.ActorId, run.InitiatingUserId, run.SubjectType, run.SubjectId, Convert.ToBase64String(run.RowVersion),
            run.StartedAt, run.CompletedAt, details);
    }

    private static JsonElement? ReadJson(string? value) => value is null ? null : JsonSerializer.Deserialize<JsonElement>(value);
    private static string InputHash(string slug, StartWorkflowRunRequest request) => ToolArgumentsHash.Compute(new Dictionary<string, object?>
    {
        ["slug"] = slug, ["versionId"] = request.WorkflowVersionId, ["subjectType"] = request.SubjectType, ["subjectId"] = request.SubjectId,
    });
    private static void CheckVersion(WorkflowRun run, string token)
    {
        if (token is null || token != Convert.ToBase64String(run.RowVersion)) throw new WorkflowConflictException("The run changed. Reload it before deciding.");
    }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The run changed concurrently. Reload it before deciding."); }
    }
}
