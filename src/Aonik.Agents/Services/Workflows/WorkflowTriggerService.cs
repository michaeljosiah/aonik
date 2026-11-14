using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerService(AgentsDbContext database, ITenantProvider tenantProvider, WorkflowAuthorization authorization,
    WorkflowApprovalAuthority authority, WorkflowExecutionAuthorization execution, WorkflowTriggerValidator validator,
    IAutomationIdentityAuthorization automation, WorkflowActionRegistry actions, IPermissionService permissions,
    IToolApprovalService approvals, IClock clock, WorkflowTriggerTaskProtector proofs,
    IConnectorService? connectors = null, IConnectorSubscriptionService? subscriptions = null)
    : IWorkflowTriggerService
{
    public async Task<IReadOnlyList<WorkflowTriggerDetail>> ListAsync(string slug, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Read", false, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        return (await database.WorkflowTriggerBindings.AsNoTracking().Where(binding => binding.TenantId == tenantProvider.GetCurrentTenantId()
            && binding.WorkflowId == workflow.Id).OrderBy(binding => binding.Name).Take(100).ToListAsync(cancellationToken)).Select(Detail).ToList();
    }

    public async Task<WorkflowTriggerDetail?> GetAsync(string slug, Guid bindingId, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Read", false, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding? binding = await FindAsync(workflow.Id, bindingId, cancellationToken);
        return binding is null ? null : Detail(binding);
    }

    internal async Task<(SaveWorkflowTriggerRequest Request, ToolApprovalPreview Preview)> PrepareSaveAsync(string slug, Guid? bindingId,
        SaveWorkflowTriggerRequest request, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Write", true, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) throw new ArgumentException("Provide a trigger name of at most 160 characters.");
        if (request.Definition is null) throw new ArgumentException("A trigger definition is required.");
        WorkflowTriggerDefinition definition = Normalize(request.Definition);
        IReadOnlyList<WorkflowDiagnostic> errors = validator.Validate(definition);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors.Select(error => error.Message)));
        WorkflowTriggerBinding? before = bindingId is { } id ? await FindAsync(workflow.Id, id, cancellationToken)
            ?? throw new KeyNotFoundException("Trigger binding not found.") : null;
        if (before is not null) CheckVersion(before, request.ExpectedVersion);
        else if (request.ExpectedVersion is not null) throw new ArgumentException("A new trigger cannot replace an existing revision.");
        SaveWorkflowTriggerRequest normalized = request with { Name = request.Name.Trim(), Definition = definition };
        return (normalized, new("Save workflow trigger draft", before is null ? null : JsonSerializer.SerializeToElement(new { before.Name, definition = Read(before.DraftJson) }, WorkflowDefinitionCodec.JsonOptions),
            JsonSerializer.SerializeToElement(new { normalized.Name, normalized.Definition }, WorkflowDefinitionCodec.JsonOptions)));
    }

    public async Task<WorkflowTriggerDetail> SaveAsync(string slug, Guid? bindingId, SaveWorkflowTriggerRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareSaveAsync(slug, bindingId, request, cancellationToken);
        Guid userId = await RequireHumanAsync("Workflow.Write", true, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding;
        if (bindingId is { } id)
        {
            binding = await FindAsync(workflow.Id, id, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
            CheckVersion(binding, prepared.Request.ExpectedVersion);
        }
        else
        {
            if (await database.WorkflowTriggerBindings.CountAsync(binding => binding.TenantId == workflow.TenantId && binding.WorkflowId == workflow.Id, cancellationToken) >= 100)
                throw new ArgumentException("A workflow supports at most one hundred trigger bindings.");
            binding = new() { TenantId = workflow.TenantId, WorkflowId = workflow.Id };
            database.WorkflowTriggerBindings.Add(binding);
        }
        binding.Name = prepared.Request.Name; binding.ConfiguredByUserId = userId;
        binding.DraftJson = JsonSerializer.Serialize(prepared.Request.Definition, WorkflowDefinitionCodec.JsonOptions);
        binding.DraftHash = WorkflowDefinitionCodec.Hash(binding.DraftJson); binding.DraftRevision = checked(binding.DraftRevision + 1);
        await SaveChangesAsync(cancellationToken);
        return Detail(binding);
    }

    public async Task DeleteAsync(string slug, Guid bindingId, string expectedVersion, CancellationToken cancellationToken = default)
    {
        _ = await PrepareDeletionAsync(slug, bindingId, expectedVersion, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding = await FindAsync(workflow.Id, bindingId, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
        database.WorkflowTriggerBindings.Remove(binding);
        await SaveChangesAsync(cancellationToken);
    }

    internal async Task<ToolApprovalPreview> PrepareDeletionAsync(string slug, Guid bindingId, string expectedVersion, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Write", true, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding = await FindAsync(workflow.Id, bindingId, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
        CheckVersion(binding, expectedVersion);
        if (binding.ActiveRevision.HasValue) throw new WorkflowConflictException("A previously activated trigger retains its history. Disable it instead of deleting it.");
        return new("Delete unused trigger draft", JsonSerializer.SerializeToElement(new { binding.Id, binding.Name, binding.DraftHash }, WorkflowDefinitionCodec.JsonOptions),
            JsonSerializer.SerializeToElement(new { deleted = true }));
    }

    public async Task<WorkflowTriggerValidation> ValidateAsync(string slug, Guid bindingId, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Read", false, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding = await FindAsync(workflow.Id, bindingId, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
        WorkflowTriggerDefinition definition = Read(binding.DraftJson);
        List<WorkflowDiagnostic> errors = validator.Validate(definition).ToList();
        if (errors.Count == 0)
        {
            try { await RequireReadyAsync(workflow, definition, cancellationToken); }
            catch (Exception exception) when (exception is PermissionDeniedException or WorkflowConflictException or ConnectorAccessException or ArgumentException or KeyNotFoundException)
            { errors.Add(new("TriggerNotReady", "definition", "The published version, actor, resource scopes, connection or tenant execution policy is not ready.")); }
        }
        return new(binding.DraftHash, errors);
    }

    internal async Task<ToolApprovalPreview> PrepareGovernanceAsync(string slug, Guid bindingId, WorkflowTriggerCommand command,
        bool disabling, CancellationToken cancellationToken = default)
    {
        await RequireHumanAsync("Workflow.Publish", true, cancellationToken);
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding = await FindAsync(workflow.Id, bindingId, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
        CheckVersion(binding, command.ExpectedVersion);
        string? expectedHash = disabling ? binding.ActiveHash : binding.DraftHash;
        if (command.DefinitionHash != expectedHash || (disabling && binding.Status != "Active")) throw new WorkflowConflictException("The trigger configuration or activation state changed. Review it again.");
        if (!disabling) await RequireReadyAsync(workflow, Read(binding.DraftJson), cancellationToken);
        return new(disabling ? "Disable this workflow trigger" : "Activate this exact workflow trigger",
            binding.ActiveJson is null ? null : JsonSerializer.SerializeToElement(new { binding.Name, binding.ActiveRevision, definition = Read(binding.ActiveJson) }, WorkflowDefinitionCodec.JsonOptions),
            disabling ? JsonSerializer.SerializeToElement(new { status = "Disabled", newAdmissions = false, pendingEffects = "blocked" })
                : JsonSerializer.SerializeToElement(new { binding.Name, revision = (binding.ActiveRevision ?? 0) + 1, definition = Read(binding.DraftJson) }, WorkflowDefinitionCodec.JsonOptions));
    }

    public async Task<WorkflowTriggerGovernanceResult> RequestAsync(string slug, Guid bindingId, WorkflowTriggerCommand command, bool disabling, CancellationToken cancellationToken = default)
    {
        ToolApprovalPreview preview = await PrepareGovernanceAsync(slug, bindingId, command, disabling, cancellationToken);
        ToolGateOutcome result = await approvals.GateAsync(new(disabling ? "aonik_request_disable_workflow_trigger" : "aonik_request_activate_workflow_trigger",
            WorkflowTriggerApprovalManifest.Options(disabling), new Dictionary<string, object?> { ["slug"] = slug, ["bindingId"] = bindingId,
                ["command"] = JsonSerializer.SerializeToElement(command, WorkflowDefinitionCodec.JsonOptions) }, preview), cancellationToken);
        if (result.Decision != ToolGateDecision.Queued || result.ProposalId is null || result.ApprovalRequestId is null) throw new InvalidOperationException("Trigger governance could not queue its durable approval.");
        return new(result.ProposalId.Value, result.ApprovalRequestId.Value, preview);
    }

    internal async Task<ProposalHandlerResult> ApplyAsync(Guid proposalId, bool disabling, CancellationToken cancellationToken)
    {
        Guid userId = await RequireHumanAsync("Workflow.Publish", true, cancellationToken);
        Proposal proposal = await database.Proposals.SingleOrDefaultAsync(proposal => proposal.TenantId == tenantProvider.GetCurrentTenantId() && proposal.Id == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Trigger proposal not found.");
        string type = disabling ? WorkflowTriggerApprovalManifest.DisableType : WorkflowTriggerApprovalManifest.ActivateType;
        if (proposal.ProposalType != type || proposal.Status is not (ProposalStatus.Approved or ProposalStatus.Applied)
            || proposal.ApprovedByUserId != userId || proposal.ApprovedAt is null || !await authority.CanDecideProposalAsync(proposal, cancellationToken))
            throw new PermissionDeniedException("Workflow.Publish", "A current human administrator must approve this exact trigger proposal.");
        if (proposal.Status == ProposalStatus.Applied)
            return JsonSerializer.Deserialize<ProposalHandlerResult>(proposal.ExecutionResultJson ?? "null", WorkflowDefinitionCodec.JsonOptions)
                ?? throw new WorkflowConflictException("The trigger's applied receipt is unavailable.");
        ToolApprovalRequest request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == proposal.TenantId && request.ProposalId == proposal.Id, cancellationToken)
            ?? throw new PermissionDeniedException("Workflow.Publish");
        if (request.RequestingUserId is not { } author || request.ExpiresAt <= clock.UtcNow
            || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Cancelled or ToolApprovalRequestStatus.Expired
            || !await authority.IsPublisherAsync(proposal.TenantId, author, cancellationToken)
            || !await permissions.HasPermissionAsync(author, "Integration.Manage", cancellationToken)) throw new PermissionDeniedException("Workflow.Publish");
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(proposal.PayloadJson);
        string argumentsHash = ToolArgumentsHash.Compute(payload.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
        if (request.ArgsHash != argumentsHash || request.ToolName != (disabling ? "aonik_request_disable_workflow_trigger" : "aonik_request_activate_workflow_trigger"))
            throw new PermissionDeniedException("Workflow.Publish", "The trigger payload no longer matches its exact reviewed arguments.");
        string slug = payload.GetProperty("slug").GetString()!;
        Guid bindingId = payload.GetProperty("bindingId").GetGuid();
        WorkflowTriggerCommand command = payload.GetProperty("command").Deserialize<WorkflowTriggerCommand>(WorkflowDefinitionCodec.JsonOptions)!;
        Workflow workflow = await WorkflowAsync(slug, cancellationToken);
        WorkflowTriggerBinding binding = await FindAsync(workflow.Id, bindingId, cancellationToken) ?? throw new KeyNotFoundException("Trigger binding not found.");
        _ = await PrepareGovernanceAsync(slug, bindingId, command, disabling, cancellationToken);
        Guid? previousTask = binding.TaskId;
        string? scheduleProof = null;
        if (disabling)
        {
            binding.Status = "Disabled"; binding.DisabledAt = clock.UtcNow; binding.DisabledByUserId = userId;
            binding.DisabledThroughRevision = Math.Max(binding.DisabledThroughRevision, binding.ActiveRevision!.Value);
        }
        else
        {
            WorkflowTriggerDefinition definition = Read(binding.DraftJson);
            binding.ActiveJson = binding.DraftJson; binding.ActiveHash = binding.DraftHash;
            binding.ActiveRevision = checked((binding.ActiveRevision ?? 0) + 1); binding.ActiveSource = definition.Source;
            binding.ActiveAutomationIdentityId = definition.AutomationIdentityId; binding.ActivationProposalId = proposal.Id;
            binding.ActivatedByUserId = userId; binding.ActivatedAt = clock.UtcNow; binding.DisabledAt = null; binding.DisabledByUserId = null;
            binding.Status = "Active"; binding.TaskId = null; binding.SubscriptionId = null; binding.LastError = null;
            if (definition.Source == "Schedule")
            {
                binding.TaskId = WorkflowTriggerTaskProtector.Identity("schedule", binding.Id, binding.ActiveRevision.Value);
                scheduleProof = proofs.ProtectSchedule(new(binding.Id, binding.ActiveRevision.Value), binding.TenantId, binding.TaskId.Value);
            }
        }
        database.EnqueueIntegrationEvent(new WorkflowTriggerChangedEvent(Guid.NewGuid(), clock.UtcNow, binding.TenantId, binding.Id,
            binding.ActiveRevision!.Value, disabling ? "Disable" : "Activate", previousTask, disabling ? null : binding.TaskId, scheduleProof));
        var receipt = new ProposalHandlerResult(true, "WorkflowTriggerBinding", binding.Id,
            disabling ? "The trigger is disabled; queued effects must revalidate it." : "The exact trigger revision is active.");
        proposal.Status = ProposalStatus.Applied;
        proposal.ExecutionResultJson = JsonSerializer.Serialize(receipt, WorkflowDefinitionCodec.JsonOptions);
        await SaveChangesAsync(cancellationToken);
        return receipt;
    }

    private async Task RequireReadyAsync(Workflow workflow, WorkflowTriggerDefinition definition, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkflowDiagnostic> errors = validator.Validate(definition);
        if (errors.Count != 0) throw new ArgumentException("The trigger definition has validation errors.");
        if (workflow.State != WorkflowStates.Active || workflow.RetiredAt.HasValue) throw new WorkflowConflictException("Publish an active workflow before activating its trigger.");
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.RequireAutomaticEnabledAsync(cancellationToken);
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == workflow.TenantId
            && version.WorkflowId == workflow.Id && version.Id == definition.WorkflowVersionId, cancellationToken) ?? throw new KeyNotFoundException("Published workflow version not found.");
        WorkflowDefinition snapshot = WorkflowRunService.ReadDefinition(version);
        if (snapshot.DefinitionKind != "Workflow") throw new WorkflowConflictException("Automatic sources require a workflow; pure decision libraries are consumed through pinned Decision nodes.");
        var actor = new WorkflowActor("AutomationIdentity", definition.AutomationIdentityId, WorkflowVersionId: version.Id);
        await execution.RequireActorAsync(actor, cancellationToken);
        foreach (Guid subjectId in definition.Subject.FixedIds) await execution.LoadFactsAsync(definition.Subject.Type, subjectId, actor, cancellationToken);
        foreach (WorkflowDefinitionNode node in snapshot.Nodes.Where(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool)))
        {
            WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
            var handler = actions.Resolve(parameters.ActionKey, parameters.ContractVersion);
            await execution.RequireModuleAsync(handler.GetType(), cancellationToken);
            await automation.RequireAsync(actor, handler.Descriptor.RequiredPermission, action: new(parameters.ActionKey, parameters.ContractVersion), cancellationToken: cancellationToken);
            if (parameters.Connection is { } connection)
            {
                if (connectors is null) throw new ArgumentException("Connector execution is unavailable.");
                await connectors.ValidateWorkflowBindingAsync(workflow.Id, connection, new(parameters.ActionKey, parameters.ContractVersion), actor, cancellationToken);
            }
        }
        if (definition.Mailbox is { } mailbox)
        {
            if (subscriptions is null) throw new ArgumentException("Mailbox synchronization is not provisioned.");
            await subscriptions.ValidateAsync(new(Guid.Empty, 0, workflow.Id, version.Id, actor.Id,
                new(mailbox.ConnectionId, mailbox.WorkflowGrantId, [new(GmailActionKeys.Search, 1)]), mailbox.Mode, mailbox.PollIntervalMinutes,
                mailbox.LabelIds, mailbox.AllowPollingFallback, mailbox.BackfillSinceUtc), cancellationToken);
        }
    }

    private async Task<Guid> RequireHumanAsync(string permission, bool managing, CancellationToken cancellationToken)
    {
        Guid userId = await authorization.RequireAsync(permission, cancellationToken);
        if (!await authority.CanReadGovernanceAsync(cancellationToken)) throw new PermissionDeniedException("Workflow.Read");
        if (managing && !await permissions.HasPermissionAsync(userId, "Integration.Manage", cancellationToken)) throw new PermissionDeniedException("Integration.Manage");
        return userId;
    }
    private async Task<Workflow> WorkflowAsync(string slug, CancellationToken cancellationToken) => await database.Workflows.SingleOrDefaultAsync(workflow => workflow.TenantId == tenantProvider.GetCurrentTenantId()
        && workflow.Slug == slug, cancellationToken) ?? throw new KeyNotFoundException("Workflow not found.");
    private Task<WorkflowTriggerBinding?> FindAsync(Guid workflowId, Guid id, CancellationToken cancellationToken) => database.WorkflowTriggerBindings.SingleOrDefaultAsync(binding => binding.TenantId == tenantProvider.GetCurrentTenantId()
        && binding.WorkflowId == workflowId && binding.Id == id, cancellationToken);
    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The trigger changed concurrently. Reload its current revision before retrying."); }
    }
    private static WorkflowTriggerDefinition Normalize(WorkflowTriggerDefinition definition) => definition with
    {
        Subject = definition.Subject is null ? null! : definition.Subject with { FixedIds = definition.Subject.FixedIds?.Order().ToList()! },
        Mailbox = definition.Mailbox is null ? null : definition.Mailbox with { LabelIds = definition.Mailbox.LabelIds?.Order(StringComparer.Ordinal).ToList()! },
    };
    internal static WorkflowTriggerDefinition Read(string json) => JsonSerializer.Deserialize<WorkflowTriggerDefinition>(json, WorkflowDefinitionCodec.JsonOptions)
        ?? throw new WorkflowConflictException("The trigger definition is invalid.");
    internal static void CheckVersion(WorkflowTriggerBinding binding, string? version)
    { if (version is null || version != Convert.ToBase64String(binding.RowVersion)) throw new WorkflowConflictException("The trigger changed. Review the current version."); }
    private static WorkflowTriggerDetail Detail(WorkflowTriggerBinding binding) => new(binding.Id, binding.WorkflowId, binding.Name, binding.Status, binding.DraftRevision,
        binding.DraftHash, Read(binding.DraftJson), binding.ActiveRevision, binding.ActiveHash, binding.ActiveJson is null ? null : Read(binding.ActiveJson),
        binding.ActivationProposalId, binding.TaskId, binding.SubscriptionId, binding.AdmittedRuns, binding.LastError, Convert.ToBase64String(binding.RowVersion));
}
