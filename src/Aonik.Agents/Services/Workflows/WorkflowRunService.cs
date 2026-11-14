using System.Text.Json;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowRunService(
    AgentsDbContext database, ITenantProvider tenantProvider, WorkflowAuthorization authorization,
    WorkflowExecutionAuthorization executionAuthorization, IClock clock, WorkflowActionRegistry? actions = null,
    WorkflowReviewerAuthorization? reviewers = null, IConnectorService? connectors = null,
    IAutomationIdentityAuthorization? automation = null, IWorkflowMailboxBindingReader? mailboxBindings = null,
    WorkflowDependencyValidator? dependencies = null, WorkflowContinuationWriter? continuations = null) : IWorkflowRunService, IWorkflowAutomationAdmission
{
    public async Task<WorkflowRunDetail> StartAsync(string slug, StartWorkflowRunRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireAsync("Workflow.Run", cancellationToken);
        var actor = new WorkflowActor("User", userId, userId);
        return await DetailAsync(await AdmitCoreAsync(slug, request, actor, userId, requireCurrent: true, cancellationToken), cancellationToken);
    }

    public Task<Guid> AdmitAsync(Guid identityId, Guid workflowVersionId, string subjectType, Guid subjectId,
        string idempotencyKey, CancellationToken cancellationToken = default)
        => AdmitAutomaticAsync(identityId, workflowVersionId, subjectType, subjectId, idempotencyKey, null, cancellationToken);

    internal Task<Guid> AdmitTriggeredAsync(Guid identityId, Guid workflowVersionId, string subjectType, Guid subjectId,
        string idempotencyKey, WorkflowTriggerSource source, CancellationToken cancellationToken = default)
        => AdmitAutomaticAsync(identityId, workflowVersionId, subjectType, subjectId, idempotencyKey, source, cancellationToken);

    private async Task<Guid> AdmitAutomaticAsync(Guid identityId, Guid workflowVersionId, string subjectType, Guid subjectId,
        string idempotencyKey, WorkflowTriggerSource? source, CancellationToken cancellationToken)
    {
        if (automation is null) throw new PermissionDeniedException("Workflow.Run");
        var actor = new WorkflowActor("AutomationIdentity", identityId, WorkflowVersionId: workflowVersionId);
        Guid configuringUserId = await automation.RequireAsync(actor, "Workflow.Run", subjectType, subjectId, cancellationToken: cancellationToken);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == tenantId && version.Id == workflowVersionId, cancellationToken)
            ?? throw new KeyNotFoundException("Published workflow version not found.");
        string slug = await database.Workflows.Where(workflow => workflow.TenantId == tenantId && workflow.Id == version.WorkflowId)
            .Select(workflow => workflow.Slug).SingleAsync(cancellationToken);
        WorkflowRun run = await AdmitCoreAsync(slug, new(workflowVersionId, subjectType, subjectId, idempotencyKey), actor,
            configuringUserId, requireCurrent: false, cancellationToken, source);
        return run.Id;
    }

    private async Task<WorkflowRun> AdmitCoreAsync(string slug, StartWorkflowRunRequest request, WorkflowActor actor,
        Guid configuringUserId, bool requireCurrent, CancellationToken cancellationToken, WorkflowTriggerSource? source = null, WorkflowChildAdmission? child = null)
    {
        await executionAuthorization.RequireActorAsync(actor, cancellationToken);
        await executionAuthorization.RequireEnabledAsync(cancellationToken);
        ValidateRequest(request);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        string inputHash = InputHash(slug, request, source, child);
        WorkflowRun? existing = await FindReplayAsync(tenantId, actor, request.IdempotencyKey, cancellationToken);
        if (existing is not null) return await ReplayAsync(existing, inputHash, actor, cancellationToken);
        (Workflow workflow, WorkflowVersion version, WorkflowFactsSnapshot facts) = await PrepareAdmissionAsync(slug, request, actor, cancellationToken, requireCurrent);
        (WorkflowTriggerBinding Binding, Guid? RootRunId, IReadOnlyList<Guid> Path)? trigger = null;
        if (source is not null)
        {
            try { trigger = await PrepareTriggerAdmissionAsync(workflow, request, actor, source, cancellationToken); }
            catch (WorkflowExecutionException exception) when (exception.Code is "TriggerSubjectAlreadyActive" or "TriggerCapacityReached" or "TriggerOccurrenceLimit")
            {
                // Another delivery may have committed this exact source between the first replay read and the capacity check.
                existing = await FindReplayAsync(tenantId, actor, request.IdempotencyKey, cancellationToken);
                if (existing is not null) return await ReplayAsync(existing, inputHash, actor, cancellationToken);
                throw;
            }
        }
        var run = new WorkflowRun
        {
            TenantId = tenantId, WorkflowId = workflow.Id, WorkflowVersionId = version.Id, DefinitionHash = version.DefinitionHash,
            Mode = "Live", ActorType = actor.Type, ActorId = actor.Id, InitiatingUserId = actor.InitiatingUserId, ConfiguredByUserId = configuringUserId,
            SubjectType = request.SubjectType, SubjectId = request.SubjectId, IdempotencyKey = request.IdempotencyKey,
            InputHash = inputHash, FactsJson = JsonSerializer.Serialize(facts, WorkflowDefinitionCodec.JsonOptions), FactsHash = WorkflowFactsHash.Compute(facts),
            Status = WorkflowRunStatuses.Queued, StartedAt = clock.UtcNow, StartedBy = $"{(actor.Type == "User" ? "manual" : "automation")}:{actor.Id:D}",
            DeadlineUtc = version.SchemaVersion >= 2 ? clock.UtcNow.AddSeconds(WorkflowControlLimits.MaximumLifetimeSeconds) : null,
        };
        if (version.SchemaVersion >= 2) run.ExecutionRootRunId = run.Id;
        if (child is not null)
        {
            run.ParentRunId = child.ParentRunId; run.ParentStepRunId = child.ParentStepRunId; run.Depth = child.Depth;
            run.ExecutionRootRunId = child.ExecutionRootRunId; run.InvocationPath = child.InvocationPath;
            run.RootRunId = child.SourceRootRunId; run.CausationRunId = child.ParentRunId; run.TriggerPathJson = child.TriggerPathJson;
            run.DeadlineUtc = child.DeadlineUtc;
            run.StartedBy = $"child:{child.ParentRunId:D}";
        }
        if (source is not null && trigger is { } admitted)
        {
            run.TriggerBindingId = source.BindingId; run.TriggerBindingRevision = source.Revision;
            run.TriggerApprovalProposalId = source.ApprovalProposalId; run.TriggerDefinitionHash = source.DefinitionHash;
            run.SourceEventId = source.SourceEventId; run.SourceOccurredAtUtc = source.OccurredAtUtc;
            run.CausationRunId = source.CausationRunId; run.RootRunId = admitted.RootRunId ?? run.Id;
            run.TriggerPathJson = JsonSerializer.Serialize(admitted.Path);
            admitted.Binding.AdmittedRuns = checked(admitted.Binding.AdmittedRuns + 1);
        }
        database.WorkflowRuns.Add(run);
        // Updating this tracked root also detects a concurrent retirement/publication during admission.
        workflow.TriggerCount = checked(workflow.TriggerCount + 1);
        List<Guid> wakeIds = [Queue(run, clock.UtcNow)];
        if (run.DeadlineUtc is { } runDeadline) wakeIds.Add(Queue(run, DateTime.SpecifyKind(runDeadline, DateTimeKind.Utc)));
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            database.Entry(run).State = EntityState.Detached;
            foreach (var entry in database.ChangeTracker.Entries<OutboxMessage>().Where(entry => wakeIds.Contains(entry.Entity.EventId)))
                entry.State = EntityState.Detached;
            await database.Entry(workflow).ReloadAsync(cancellationToken);
            if (trigger is { } raced) await database.Entry(raced.Binding).ReloadAsync(cancellationToken);
            existing = await FindReplayAsync(tenantId, actor, request.IdempotencyKey, cancellationToken);
            if (existing is null) throw;
            return await ReplayAsync(existing, inputHash, actor, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The workflow changed during admission. Retry the same admission key."); }
        return run;
    }

    internal async Task<WorkflowRun> AdmitChildAsync(WorkflowRun parent, WorkflowStepRun step, WorkflowVersionPin pin, Guid invoiceId,
        DateTime? deadlineUtc = null, CancellationToken cancellationToken = default)
    {
        if (dependencies is null || continuations is null || parent.TenantId != tenantProvider.GetCurrentTenantId() || step.RunId != parent.Id || step.TenantId != parent.TenantId
            || parent.Depth >= WorkflowControlLimits.MaximumNestingDepth || parent.DeadlineUtc is null
            || !await database.WorkflowRuns.AsNoTracking().AnyAsync(run => run.TenantId == parent.TenantId && run.Id == parent.Id
                && run.Status == WorkflowRunStatuses.Running && run.LeaseId == parent.LeaseId && run.LeaseExpiresAt > clock.UtcNow, cancellationToken))
            throw new WorkflowExecutionException("ChildAdmissionAuthorityChanged");
        await executionAuthorization.RequireRunAsync(parent, cancellationToken);
        WorkflowVersion version = await dependencies.RequirePinAsync(pin, cancellationToken);
        string slug = await database.Workflows.Where(workflow => workflow.TenantId == parent.TenantId && workflow.Id == version.WorkflowId)
            .Select(workflow => workflow.Slug).SingleAsync(cancellationToken);
        DateTime deadline = DateTime.SpecifyKind(new[] { parent.DeadlineUtc.Value, deadlineUtc ?? parent.DeadlineUtc.Value }.Min(), DateTimeKind.Utc);
        if (deadline <= clock.UtcNow) throw new WorkflowExecutionException("ChildDeadlineExceeded");
        var child = new WorkflowChildAdmission(parent.Id, step.Id, parent.ExecutionRootRunId ?? parent.Id, parent.Depth + 1,
            $"{parent.InvocationPath}/{step.NodeKey}:{step.InvocationPath}", deadline, parent.RootRunId ?? parent.Id, parent.TriggerPathJson);
        if (child.InvocationPath.Length > 1024) throw new WorkflowExecutionException("InvocationPathLimit");
        // The same owning save fences cancellation while committing the child and its one wake-up.
        parent.PendingStepRunId = step.Id;
        database.Entry(parent).Property(run => run.PendingStepRunId).IsModified = true;
        return await AdmitCoreAsync(slug, new(pin.VersionId, "Invoice", invoiceId, $"child-{step.Id:N}"),
            WorkflowExecutionAuthorization.Actor(parent) with { WorkflowVersionId = pin.VersionId }, parent.ConfiguredByUserId!.Value,
            requireCurrent: false, cancellationToken, child: child);
    }

    private async Task<(WorkflowTriggerBinding, Guid?, IReadOnlyList<Guid>)> PrepareTriggerAdmissionAsync(Workflow workflow,
        StartWorkflowRunRequest request, WorkflowActor actor, WorkflowTriggerSource source, CancellationToken cancellationToken)
    {
        await executionAuthorization.RequireAutomaticEnabledAsync(cancellationToken);
        WorkflowTriggerBinding binding = await database.WorkflowTriggerBindings.SingleOrDefaultAsync(binding => binding.TenantId == workflow.TenantId
            && binding.Id == source.BindingId && binding.WorkflowId == workflow.Id, cancellationToken) ?? throw new WorkflowExecutionException("TriggerUnavailable");
        if (binding.Status != "Active" || binding.ActiveRevision != source.Revision || binding.ActiveHash != source.DefinitionHash
            || binding.ActivationProposalId != source.ApprovalProposalId || binding.ActiveJson is null
            || WorkflowDefinitionCodec.Hash(binding.ActiveJson) != source.DefinitionHash || source.SourceEventId == Guid.Empty
            || source.OccurredAtUtc.Kind != DateTimeKind.Utc || source.OccurredAtUtc > clock.UtcNow.AddMinutes(5)
            || !await database.Proposals.AsNoTracking().AnyAsync(proposal => proposal.TenantId == workflow.TenantId
                && proposal.Id == source.ApprovalProposalId && proposal.Status == ProposalStatus.Applied && proposal.ApprovedByUserId != null
                && proposal.ProposalType == Framework.WorkflowTriggerApprovalManifest.ActivateType, cancellationToken))
            throw new WorkflowExecutionException("TriggerApprovalMismatch");
        WorkflowTriggerDefinition definition = WorkflowTriggerService.Read(binding.ActiveJson);
        if (definition.Mailbox is not null)
        {
            if (mailboxBindings is null) throw new WorkflowExecutionException("MailboxAuthorizationUnavailable");
            _ = await mailboxBindings.RequireActiveAsync(binding.Id, source.Revision, cancellationToken);
        }
        if (actor.Type != "AutomationIdentity" || actor.Id != definition.AutomationIdentityId || request.WorkflowVersionId != definition.WorkflowVersionId
            || request.SubjectType != definition.Subject.Type || (definition.Subject.Mapping == "Fixed" && !definition.Subject.FixedIds.Contains(request.SubjectId)))
            throw new WorkflowExecutionException("TriggerScopeMismatch");
        if (source.SelfGenerated && definition.Limits.IgnoreSelfGenerated) throw new WorkflowExecutionException("SelfGeneratedTriggerIgnored");
        if (definition.Schedule is { } schedule)
        {
            IQueryable<WorkflowRun> occurrences = database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == workflow.TenantId
                && run.TriggerBindingId == binding.Id && run.TriggerBindingRevision == source.Revision);
            if (!await occurrences.AnyAsync(run => run.SourceEventId == source.SourceEventId, cancellationToken)
                && await occurrences.Select(run => run.SourceEventId).Distinct().CountAsync(cancellationToken) >= schedule.MaximumOccurrences)
                throw new WorkflowExecutionException("TriggerOccurrenceLimit");
        }
        Guid? rootRunId = null;
        List<Guid> path = [];
        if (source.CausationRunId is { } causeId)
        {
            WorkflowRun parent = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(run => run.TenantId == workflow.TenantId && run.Id == causeId, cancellationToken)
                ?? throw new WorkflowExecutionException("TriggerCausationUnavailable");
            rootRunId = parent.RootRunId ?? parent.Id;
            path = JsonSerializer.Deserialize<List<Guid>>(parent.TriggerPathJson) ?? [];
        }
        if (path.Contains(binding.Id) || path.Count >= definition.Limits.MaximumCausationDepth) throw new WorkflowExecutionException("TriggerCausationLimit");
        path.Add(binding.Id);
        IQueryable<WorkflowRun> active = database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == workflow.TenantId && run.TriggerBindingId == binding.Id
            && run.Status != WorkflowRunStatuses.Completed && run.Status != WorkflowRunStatuses.Cancelled && run.Status != WorkflowRunStatuses.Failed);
        if (await active.AnyAsync(run => run.SubjectType == request.SubjectType && run.SubjectId == request.SubjectId, cancellationToken))
            throw new WorkflowExecutionException("TriggerSubjectAlreadyActive");
        if (await active.CountAsync(cancellationToken) >= definition.Limits.MaximumConcurrentRuns
            || await database.WorkflowRuns.AsNoTracking().CountAsync(run => run.TenantId == workflow.TenantId && run.TriggerBindingId == binding.Id
                && run.StartedAt >= clock.UtcNow.AddHours(-1), cancellationToken) >= definition.Limits.MaximumStartsPerHour)
            throw new WorkflowExecutionException("TriggerCapacityReached");
        return (binding, rootRunId, path);
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
        string slug, StartWorkflowRunRequest request, WorkflowActor actor, CancellationToken cancellationToken, bool requireCurrent = true)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        Workflow workflow = await database.Workflows.SingleOrDefaultAsync(workflow => workflow.TenantId == tenantId && workflow.Slug == slug, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow not found.");
        if ((requireCurrent && workflow.PublishedVersionId != request.WorkflowVersionId) || workflow.RetiredAt.HasValue || workflow.State != WorkflowStates.Active)
            throw new WorkflowConflictException("Only the current published, non-retired workflow version can admit a new run.");
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == tenantId
            && version.WorkflowId == workflow.Id && version.Id == request.WorkflowVersionId, cancellationToken);
        WorkflowDefinition definition = ReadDefinition(version);
        if (definition.DefinitionKind != "Workflow") throw new WorkflowConflictException("A pure decision library is consumed by a pinned Decision node; it cannot admit an effectful workflow run.");
        if (definition.Nodes.Any(node => WorkflowControlContracts.IsComposition(node) || WorkflowDecisionLibraries.IsReference(node)))
        {
            if (dependencies is null) throw new WorkflowConflictException("Workflow composition validation is unavailable.");
            _ = await dependencies.ValidateAsync(definition, workflow.Id, cancellationToken);
        }
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
        if (run.Status is not (WorkflowRunStatuses.WaitingForConnection or WorkflowRunStatuses.NeedsAttention or WorkflowRunStatuses.WaitingForTimer))
            throw new WorkflowConflictException("Only a held run or a due timer can be explicitly resumed.");
        if (run.Status == WorkflowRunStatuses.WaitingForTimer && !await database.WorkflowStepRuns.AsNoTracking().AnyAsync(step => step.TenantId == run.TenantId
                && step.RunId == run.Id && step.Id == run.PendingStepRunId && step.Status == "WaitingForTimer" && step.DeadlineUtc <= clock.UtcNow, cancellationToken))
            throw new WorkflowConflictException("The recorded workflow timer is not due. Resuming cannot shorten its wait.");
        await executionAuthorization.RequireRunAsync(run, cancellationToken);
        WorkflowFactsSnapshot current = await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value,
            WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != run.FactsHash && !await HasAppliedPendingOperationAsync(run, cancellationToken))
            throw new WorkflowConflictException("The workflow subject changed. Review a new run instead of replacing its approved facts.");
        if (run.Status == WorkflowRunStatuses.WaitingForConnection)
        {
            WorkflowStepRun pending = await database.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.TenantId == run.TenantId
                && step.RunId == run.Id && step.Id == run.PendingStepRunId, cancellationToken);
            WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == run.TenantId
                && version.Id == run.WorkflowVersionId && version.WorkflowId == run.WorkflowId, cancellationToken);
            if (version.DefinitionHash != run.DefinitionHash) throw new WorkflowConflictException("The pinned definition changed.");
            WorkflowToolParameters parameters = ReadDefinition(version).Nodes.Single(node => node.Key == pending.NodeKey).Parameters
                .Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
            if (connectors is null || parameters.Connection is null) throw new WorkflowConflictException("The published connector binding is unavailable.");
            await connectors.ValidateWorkflowBindingAsync(run.WorkflowId, parameters.Connection, new(parameters.ActionKey, parameters.ContractVersion),
                WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        }
        run.Status = WorkflowRunStatuses.Queued;
        run.ErrorCode = null;
        run.Continuation++;
        Queue(run, clock.UtcNow);
        if (run.DeadlineUtc is { } runDeadline) Queue(run, DateTime.SpecifyKind(runDeadline, DateTimeKind.Utc));
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
        if (run.ParentRunId.HasValue)
        {
            if (continuations is null) throw new WorkflowConflictException("Child continuation is unavailable.");
            await continuations.AfterChildAsync(run, cancellationToken);
        }
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
        if (version.PublishedAt is null || version.SchemaVersion is not (1 or 2) || version.DefinitionJson is not { } json
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

    private Task<WorkflowRun?> FindReplayAsync(Guid tenantId, WorkflowActor actor, string key, CancellationToken cancellationToken) => database.WorkflowRuns.AsNoTracking()
        .SingleOrDefaultAsync(run => run.TenantId == tenantId && run.ActorType == actor.Type && run.ActorId == actor.Id && run.IdempotencyKey == key, cancellationToken);

    private async Task<WorkflowRun> ReplayAsync(WorkflowRun run, string inputHash, WorkflowActor actor, CancellationToken cancellationToken)
    {
        if (run.InputHash != inputHash) throw new WorkflowConflictException("The admission key is already bound to different input.");
        await executionAuthorization.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, actor, cancellationToken);
        return run;
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
                WorkflowHumanTaskView? human = null;
                if (request.ToolName == WorkflowHumanTaskContract.ToolName && step.ArgumentsJson is { } args)
                {
                    WorkflowHumanParameters parameters = JsonSerializer.Deserialize<JsonElement>(args).GetProperty("parameters")
                        .Deserialize<WorkflowHumanParameters>(WorkflowDefinitionCodec.JsonOptions)!;
                    human = new(parameters.Purpose, parameters.Choices, request.DecidedByUserId, ReadJson(request.ResponseJson), request.ResponseHash);
                }
                approval = new(request.Id, request.RiskTier, request.ActionKind, request.Status.ToString(), Utc(request.ExpiresAt)!.Value, canDecide,
                    request.PreviewJson is { } preview ? JsonSerializer.Deserialize<ToolApprovalPreview>(preview, WorkflowDefinitionCodec.JsonOptions) : null, human);
            }
            details.Add(new(step.Id, step.NodeKey, step.InvocationPath, step.Status, step.ActionKey, step.ActionVersion,
                step.SelectedRoute, ReadJson(step.DecisionTraceJson), ReadJson(step.ResultJson), step.ApprovalRequestId, step.ProposalId, step.AiRunId, step.ErrorCode, approval, Utc(step.DeadlineUtc)));
        }
        return new(run.Id, run.WorkflowId, run.WorkflowVersionId, run.DefinitionHash, run.Status, run.Outcome, run.ErrorCode,
            run.ActorType, run.ActorId, run.InitiatingUserId, run.SubjectType, run.SubjectId, Convert.ToBase64String(run.RowVersion),
            run.StartedAt, run.CompletedAt, details, run.ConfiguredByUserId, run.TriggerBindingId is { } bindingId
                ? new(bindingId, run.TriggerBindingRevision!.Value, run.SourceEventId!.Value, DateTime.SpecifyKind(run.SourceOccurredAtUtc!.Value, DateTimeKind.Utc), run.CausationRunId, run.RootRunId) : null,
            Utc(run.DeadlineUtc), Utc(steps.SingleOrDefault(step => step.Id == run.PendingStepRunId)?.DeadlineUtc), run.ParentRunId, run.ExecutionRootRunId, run.InvocationPath,
            await database.WorkflowRuns.AsNoTracking().Where(child => child.TenantId == run.TenantId && child.ParentRunId == run.Id)
                .OrderBy(child => child.InvocationPath).Select(child => new WorkflowChildRunSummary(child.Id, child.WorkflowVersionId!.Value,
                    child.InvocationPath, child.Status, child.Outcome, child.ErrorCode, child.SubjectId!.Value)).ToListAsync(cancellationToken));
    }

    private static DateTime? Utc(DateTime? value) => value is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null;
    private static JsonElement? ReadJson(string? value) => value is null ? null : JsonSerializer.Deserialize<JsonElement>(value);
    private static string InputHash(string slug, StartWorkflowRunRequest request, WorkflowTriggerSource? source = null, WorkflowChildAdmission? child = null)
    {
        var values = new Dictionary<string, object?> { ["slug"] = slug, ["versionId"] = request.WorkflowVersionId, ["subjectType"] = request.SubjectType, ["subjectId"] = request.SubjectId };
        if (source is not null) values["source"] = JsonSerializer.SerializeToElement(source, WorkflowDefinitionCodec.JsonOptions);
        if (child is not null) values["child"] = JsonSerializer.SerializeToElement(child, WorkflowDefinitionCodec.JsonOptions);
        return ToolArgumentsHash.Compute(values);
    }
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

internal sealed record WorkflowChildAdmission(Guid ParentRunId, Guid ParentStepRunId, Guid ExecutionRootRunId, int Depth, string InvocationPath, DateTime DeadlineUtc,
    Guid SourceRootRunId, string TriggerPathJson);
