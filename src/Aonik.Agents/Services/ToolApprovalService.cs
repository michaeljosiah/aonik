using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services;

/// <summary>
/// Server-side front door (Spec 032 §7.5) the <c>ApprovalGatedAIFunction</c> decorator delegates to
/// so tier routing — and the identity / tenant / freshness / replay enforcement — lives in one
/// testable place. Every gated call persists a durable <see cref="ToolApprovalRequest"/>:
/// <list type="bullet">
///   <item><strong>Low</strong> — created already <see cref="ToolApprovalRequestStatus.Approved"/>
///   (and consumed); returns <see cref="ToolGateDecision.ApprovedInline"/> so the reversible write
///   runs in-band, audited.</item>
///   <item><strong>Medium</strong> — if an approved, unconsumed, unexpired request already matches
///   this call (bound by args-hash) it is consumed and we return ApprovedInline so the inner tool
///   runs once; otherwise a Pending request is created and we return
///   <see cref="ToolGateDecision.PendingApproval"/> so the agent re-invokes after the user decides.</item>
///   <item><strong>High</strong> — the arguments are marshalled into a durable <c>Proposal</c>
///   (Status = Proposed, RiskTier = "High"), the request is linked to it, and we return
///   <see cref="ToolGateDecision.Queued"/>. The inner money call is <strong>never</strong> invoked
///   here; the matching <c>IProposalHandler</c> is the only path to it, after approval.</item>
/// </list>
/// <para>
/// <see cref="DecideAsync"/> is the single decision authority: a decision arriving over any
/// transport is validated here (deciding user equals the requesting user for consumer flows;
/// tenant is structural via the store's query filter; expiry; single-use status) before it has any
/// effect. A High decision is routed through the policy-checked
/// <see cref="IProposalApprovalService.ApproveAsync"/> — the same money path the approvals queue
/// uses — so "in-session = queue".
/// </para>
/// <para>
/// Fail-closed: any condition that prevents creating a proper tenant-scoped row — a High tool with
/// no <c>ProposalType</c>, or no resolvable tenant for a Medium/High call — returns
/// <see cref="ToolGateDecision.Refused"/> rather than letting the mutation run ungated.
/// </para>
/// </summary>
internal sealed class ToolApprovalService : IToolApprovalService
{
    /// <summary>
    /// How long a Pending request stays decidable / consumable. Bounds the replay window: a stale
    /// approval cannot be consumed after this. Generous enough for a human to read a card and click,
    /// short enough to stay a tight window for the durable multi-turn confirm.
    /// </summary>
    private static readonly TimeSpan PendingApprovalWindow = TimeSpan.FromMinutes(15);

    private const string HighRiskTier = "High";

    private readonly IToolApprovalRequestStore _requestStore;
    private readonly IAgentProposalStore _proposalStore;
    private readonly IProposalApprovalService _proposalApprovalService;
    private readonly IProposalApprovalPolicy _proposalApprovalPolicy;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;
    private readonly ChatRoomInvocationGuard? _roomContext;
    private readonly IChatRoomApprovalAuthority? _roomApprovalAuthority;
    private readonly WorkflowApprovalAuthority? _workflowAuthority;
    private readonly WorkflowInvocationContext? _workflowContext;
    private readonly AgentsDbContext? _workflowDatabase;
    private readonly WorkflowReviewerAuthorization? _workflowReviewers;
    private readonly WorkflowActionRegistry? _workflowActions;
    private readonly WorkflowContinuationWriter? _workflowContinuations;

    public ToolApprovalService(
        IToolApprovalRequestStore requestStore,
        IAgentProposalStore proposalStore,
        IProposalApprovalService proposalApprovalService,
        IProposalApprovalPolicy proposalApprovalPolicy,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock,
        ChatRoomInvocationGuard? roomContext = null,
        IChatRoomApprovalAuthority? roomApprovalAuthority = null,
        WorkflowApprovalAuthority? workflowAuthority = null,
        WorkflowInvocationContext? workflowContext = null,
        AgentsDbContext? workflowDatabase = null,
        WorkflowReviewerAuthorization? workflowReviewers = null,
        WorkflowActionRegistry? workflowActions = null,
        WorkflowContinuationWriter? workflowContinuations = null)
    {
        _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
        _proposalStore = proposalStore ?? throw new ArgumentNullException(nameof(proposalStore));
        _proposalApprovalService = proposalApprovalService ?? throw new ArgumentNullException(nameof(proposalApprovalService));
        _proposalApprovalPolicy = proposalApprovalPolicy ?? throw new ArgumentNullException(nameof(proposalApprovalPolicy));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
        _currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _roomContext = roomContext;
        _roomApprovalAuthority = roomApprovalAuthority;
        _workflowAuthority = workflowAuthority;
        _workflowContext = workflowContext;
        _workflowDatabase = workflowDatabase;
        _workflowReviewers = workflowReviewers;
        _workflowActions = workflowActions;
        _workflowContinuations = workflowContinuations;
    }

    public async Task<ToolGateOutcome> GateAsync(
        ToolGateContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options;
        var actionKind = options.ActionKind ?? context.ToolName;
        var now = _clock.UtcNow;

        // Snapshot the model-supplied arguments once: it is both the audit payload (ArgumentsRedactedJson)
        // and — for High — the proposal payload the IProposalHandler reads back by the original keys.
        var argumentsJson = JsonSerializer.Serialize(new Dictionary<string, object?>(context.Arguments));
        var argsHash = ToolArgumentsHash.Compute(context.Arguments);

        var hasTenant = _tenantProvider.TryGetCurrentTenantId(out var tenantId);
        var requestingUserId = _currentUserProvider.TryGetCurrentUserId(out var uid) ? uid : (Guid?)null;

        if (_workflowContext?.Step is not null)
        {
            ToolGateOutcome? existing = await GateExistingWorkflowActionAsync(context, argsHash, cancellationToken);
            if (existing is not null) return existing;
            requestingUserId = _workflowContext.Run!.InitiatingUserId;
        }

        return options.Tier switch
        {
            ToolApprovalTier.Low =>
                await GateLowAsync(context, options, actionKind, argumentsJson, argsHash, hasTenant, tenantId, requestingUserId, now, cancellationToken)
                    .ConfigureAwait(false),
            ToolApprovalTier.Medium =>
                await GateMediumAsync(context, options, actionKind, argumentsJson, argsHash, hasTenant, tenantId, requestingUserId, now, cancellationToken)
                    .ConfigureAwait(false),
            ToolApprovalTier.High =>
                await GateHighAsync(context, options, actionKind, argumentsJson, argsHash, hasTenant, tenantId, requestingUserId, now, cancellationToken)
                    .ConfigureAwait(false),
            _ => new ToolGateOutcome(ToolGateDecision.Refused, Summary: actionKind, Reason: "Unknown tier."),
        };
    }

    private async Task<ToolGateOutcome?> GateExistingWorkflowActionAsync(ToolGateContext context, string argsHash, CancellationToken cancellationToken)
    {
        var step = _workflowContext!.Step!;
        var run = _workflowContext.Run;
        if (_workflowDatabase is null || _workflowActions is null || run is null
            || !_tenantProvider.TryGetCurrentTenantId(out Guid tenantId) || tenantId != run.TenantId
            || step.TenantId != tenantId || step.RunId != run.Id || step.ArgumentsHash != argsHash)
            return new(ToolGateDecision.Refused, Reason: "The workflow invocation identity is invalid.");
        var descriptor = _workflowActions.Resolve(step.ActionKey!, step.ActionVersion!.Value).Descriptor;
        if (descriptor.ToolName != context.ToolName || descriptor.RequiredPermission != step.RequiredPermission || descriptor.Approval is not { } required
            || context.Options.Tier != required.Tier || context.Options.ProposalType != required.ProposalType
            || !await _workflowDatabase.WorkflowRuns.AsNoTracking().AnyAsync(current => current.TenantId == tenantId && current.Id == run.Id
                && current.Status == Entities.Workflows.WorkflowRunStatuses.Running && current.LeaseId == _workflowContext.LeaseId
                && current.LeaseExpiresAt > _clock.UtcNow, cancellationToken))
            return new(ToolGateDecision.Refused, Reason: "The workflow action classification or execution lease changed.");
        ToolApprovalRequest? request = await _workflowDatabase.ToolApprovalRequests.SingleOrDefaultAsync(
            request => request.TenantId == tenantId && request.WorkflowStepRunId == step.Id, cancellationToken);
        if (request is null) return null;
        await _workflowDatabase.Entry(request).ReloadAsync(cancellationToken);
        if (request.ArgsHash != argsHash || request.ToolName != context.ToolName || request.RiskTier != required.Tier.ToString())
            return new(ToolGateDecision.Refused, Reason: "The approval belongs to different workflow arguments.");
        if (request.ProposalId is { } proposalId)
        {
            ProposalStatus status = await _workflowDatabase.Proposals.Where(proposal => proposal.TenantId == tenantId && proposal.Id == proposalId)
                .Select(proposal => proposal.Status).SingleAsync(cancellationToken);
            return status is ProposalStatus.Rejected or ProposalStatus.Failed
                ? new(ToolGateDecision.Refused, request.Id, proposalId, Reason: "The workflow proposal was rejected or failed.")
                : new(ToolGateDecision.Queued, request.Id, proposalId, request.ActionKind);
        }
        if (required.Tier == ToolApprovalTier.Low && request.Status == ToolApprovalRequestStatus.Approved)
            return new(ToolGateDecision.ApprovedInline, request.Id, Summary: request.ActionKind);
        if (request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Expired or ToolApprovalRequestStatus.Cancelled || request.ExpiresAt <= _clock.UtcNow)
            return new(ToolGateDecision.Refused, request.Id, Reason: "The workflow approval was rejected or expired.");
        if (request.Status == ToolApprovalRequestStatus.Pending)
            return new(ToolGateDecision.PendingApproval, request.Id, Summary: request.ActionKind);
        if (_workflowReviewers is null || request.DecidedByUserId is not { } reviewer)
            return new(ToolGateDecision.Refused, request.Id, Reason: "No authorized human decided this workflow action.");
        await _workflowReviewers.RequireAsync(request, reviewer, cancellationToken);
        if (request.ConsumedAt is null)
        {
            request.ConsumedAt = _clock.UtcNow;
            await _requestStore.SaveChangesAsync(cancellationToken);
        }
        // A replay remains the same approved operation. The runner must reconcile before retrying the handler.
        return new(ToolGateDecision.ApprovedInline, request.Id, Summary: request.ActionKind);
    }

    private async Task<ToolGateOutcome> GateLowAsync(
        ToolGateContext context, ToolApprovalOptions options, string actionKind,
        string argumentsJson, string argsHash, bool hasTenant, Guid tenantId, Guid? requestingUserId,
        DateTime now, CancellationToken cancellationToken)
    {
        // Low is a reversible personal-state write: it runs in-band regardless. We still record a
        // durable Approved+consumed request when a tenant is in scope so the "every mutation is
        // audited" invariant holds; with no tenant we cannot write the tenant-scoped row, so the
        // lightweight audit sink in the decorator is the only record and the write still proceeds.
        if (!hasTenant)
        {
            return new ToolGateOutcome(ToolGateDecision.ApprovedInline, Summary: actionKind);
        }

        var request = NewRequest(context, options, actionKind, argumentsJson, argsHash, tenantId, requestingUserId, now);
        request.Status = ToolApprovalRequestStatus.Approved;
        request.DecidedByUserId = requestingUserId;
        request.DecidedAt = now;
        request.ConsumedAt = now;
        request.ExpiresAt = now; // already consumed; nothing to expire
        await _requestStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return new ToolGateOutcome(ToolGateDecision.ApprovedInline, ApprovalRequestId: request.Id, Summary: actionKind);
    }

    private async Task<ToolGateOutcome> GateMediumAsync(
        ToolGateContext context, ToolApprovalOptions options, string actionKind,
        string argumentsJson, string argsHash, bool hasTenant, Guid tenantId, Guid? requestingUserId,
        DateTime now, CancellationToken cancellationToken)
    {
        // No tenant ⇒ no durable row to track the decision ⇒ fail closed (the inner write must not run).
        if (!hasTenant)
        {
            return new ToolGateOutcome(
                ToolGateDecision.Refused, Summary: actionKind,
                Reason: $"No current tenant is available to gate '{context.ToolName}'.");
        }

        // Resubmit path: a server-validated approval for these exact arguments already exists.
        // Consume it (single-use) and let the inner tool run once.
        var approved = await _requestStore
            .FindConsumableApprovedAsync(tenantId, requestingUserId, context.ToolName, argsHash, now, cancellationToken, _roomContext?.RoomId?.ToString())
            .ConfigureAwait(false);
        if (approved is not null)
        {
            if (WorkflowApprovalAuthority.IsWorkflowMutation(context.ToolName)
                && (_workflowAuthority is null || !await _workflowAuthority.CanDecideRequestAsync(approved, cancellationToken)))
                return new ToolGateOutcome(ToolGateDecision.Refused, Reason: "Workflow execution authority changed.");
            if (_roomApprovalAuthority is not null && !await _roomApprovalAuthority.CanDecideAsync(approved.AiRunId, cancellationToken))
                return new ToolGateOutcome(ToolGateDecision.Refused, Reason: "Room execution authority changed.");
            approved.ConsumedAt = now;
            await _requestStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new ToolGateOutcome(ToolGateDecision.ApprovedInline, ApprovalRequestId: approved.Id, Summary: actionKind);
        }

        // First call: create a Pending request and refuse the in-band run. The decorator surfaces a
        // card carrying this id; the user decides via DecideAsync; the agent re-invokes (above).
        var request = NewRequest(context, options, actionKind, argumentsJson, argsHash, tenantId, requestingUserId, now);
        request.Status = ToolApprovalRequestStatus.Pending;
        request.ExpiresAt = now.Add(PendingApprovalWindow);
        StageWorkflowExpiry(request);
        await _requestStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return new ToolGateOutcome(ToolGateDecision.PendingApproval, ApprovalRequestId: request.Id, Summary: actionKind);
    }

    private async Task<ToolGateOutcome> GateHighAsync(
        ToolGateContext context, ToolApprovalOptions options, string actionKind,
        string argumentsJson, string argsHash, bool hasTenant, Guid tenantId, Guid? requestingUserId,
        DateTime now, CancellationToken cancellationToken)
    {
        // A High tool must declare the durable proposal type it maps to. Missing = misconfigured.
        if (string.IsNullOrWhiteSpace(options.ProposalType))
        {
            return new ToolGateOutcome(
                ToolGateDecision.Refused, Summary: actionKind,
                Reason: $"High-tier tool '{context.ToolName}' has no ProposalType, so it cannot be " +
                        "marshalled into a durable proposal.");
        }

        if (!hasTenant)
        {
            return new ToolGateOutcome(
                ToolGateDecision.Refused, Summary: actionKind,
                Reason: $"No current tenant is available to scope the proposal for '{context.ToolName}'.");
        }

        // Marshal into a durable Proposal — the only execution record for a money call.
        var proposalId = Guid.NewGuid();
        var proposalRequest = new AgentProposalCreateRequest(
            Id: proposalId,
            TenantId: tenantId,
            ProposalType: options.ProposalType!,
            ProposedByAgentId: Guid.Empty,
            AiRunId: _workflowContext?.Step?.AiRunId ?? _roomContext?.AiRunId,
            ImpactSummary: context.Preview?.Title ?? actionKind,
            RiskTier: HighRiskTier,
            PayloadJson: argumentsJson,
            WorkflowStepRunId: _workflowContext?.Step?.Id);

        // Durable correlation row, linked to the proposal. It is not consumed for an inline re-run
        // (the proposal pipeline executes High), so it stays Pending until DecideAsync / the
        // approvals queue resolves the proposal.
        var request = NewRequest(context, options, actionKind, argumentsJson, argsHash, tenantId, requestingUserId, now);
        request.Status = ToolApprovalRequestStatus.Pending;
        request.ExpiresAt = now.Add(PendingApprovalWindow);
        request.ProposalId = proposalId;
        if (_workflowContext?.Step is { } step) step.ProposalId = proposalId;
        StageWorkflowExpiry(request);
        if (_workflowContext?.Step is not null)
        {
            // One owning-context save commits the proposal, request, step links and deadline together.
            _workflowDatabase!.Proposals.Add(AgentProposalStore.CreateEntity(proposalRequest));
            _workflowDatabase.ToolApprovalRequests.Add(request);
            await _workflowDatabase.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await _proposalStore.CreateManyAsync(new[] { proposalRequest }, cancellationToken).ConfigureAwait(false);
            await _requestStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return new ToolGateOutcome(
            ToolGateDecision.Queued, ApprovalRequestId: request.Id, ProposalId: proposalId, Summary: actionKind);
    }

    public async Task<ToolApprovalDecisionResult> DecideAsync(
        Guid approvalRequestId,
        ToolApprovalDecisionInput decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        // A decision with no authenticated user has no authority — never decide anonymously.
        if (!_currentUserProvider.TryGetCurrentUserId(out var decidingUserId))
        {
            return new ToolApprovalDecisionResult(
                approvalRequestId, ToolApprovalDecisionOutcome.Forbidden, ProposalId: null,
                Message: "No authenticated user is making the decision.");
        }

        // The store read is tenant-scoped by the query filter, so a cross-tenant id returns null —
        // structural enforcement of the §12 tenant boundary.
        var request = await _requestStore.GetByIdAsync(approvalRequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return new ToolApprovalDecisionResult(
                approvalRequestId, ToolApprovalDecisionOutcome.NotFound, ProposalId: null,
                Message: "No pending approval request with that id is visible to you.");
        }

        var now = _clock.UtcNow;
        if (request.WorkflowStepRunId.HasValue && _workflowContinuations is null)
            return new(request.Id, ToolApprovalDecisionOutcome.Forbidden, request.ProposalId, "Workflow continuation is unavailable.");

        // Expiry takes precedence: a request past its window can no longer be decided.
        if (request.Status == ToolApprovalRequestStatus.Pending && now > request.ExpiresAt)
        {
            request.Status = ToolApprovalRequestStatus.Expired;
            if (request.WorkflowStepRunId.HasValue) await _workflowContinuations!.AfterDecisionAsync(request, cancellationToken);
            await _requestStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new ToolApprovalDecisionResult(
                request.Id, ToolApprovalDecisionOutcome.Expired, request.ProposalId,
                Message: "This approval request has expired. Ask the agent to prepare the action again.");
        }

        if (request.Status != ToolApprovalRequestStatus.Pending)
        {
            return new ToolApprovalDecisionResult(
                request.Id, ToolApprovalDecisionOutcome.AlreadyDecided, request.ProposalId,
                Message: $"This request was already {request.Status} and cannot be decided again.");
        }

        bool workflowGovernance = WorkflowApprovalAuthority.IsGovernanceTool(request.ToolName) || request.WorkflowStepRunId.HasValue;
        if (((WorkflowApprovalAuthority.IsWorkflowMutation(request.ToolName) || request.WorkflowStepRunId.HasValue) && _workflowAuthority is null)
            || (_workflowAuthority is not null && !await _workflowAuthority.CanDecideRequestAsync(request, cancellationToken)))
            return new ToolApprovalDecisionResult(request.Id, ToolApprovalDecisionOutcome.Forbidden, request.ProposalId,
                "Current workflow permissions do not permit this decision.");
        if (!workflowGovernance && _roomApprovalAuthority is not null && !await _roomApprovalAuthority.CanDecideAsync(request.AiRunId, cancellationToken))
        {
            return new ToolApprovalDecisionResult(request.Id, ToolApprovalDecisionOutcome.Forbidden, request.ProposalId,
                "Current room access does not permit deciding this action.");
        }

        // Consumer self-approval (§12): when the request records a requesting user, the deciding
        // user must be that same user. B2B operator approval (a different authorised user) is a
        // deferred policy rule; until it lands, equality is the safe default.
        if (!workflowGovernance && request.RequestingUserId is { } requestingUserId && requestingUserId != decidingUserId)
        {
            return new ToolApprovalDecisionResult(
                request.Id, ToolApprovalDecisionOutcome.Forbidden, request.ProposalId,
                Message: "Only the user who requested the action may approve it.");
        }

        if (decision.Decision == ToolApprovalDecisionType.Reject)
        {
            // High: dismiss the linked proposal too, so a money movement the user declined in-session
            // does not linger as Proposed in the approvals queue (where an operator could later approve
            // it). Keeping the request and the proposal in lock-step is the whole point of routing the
            // decision through here rather than the bare proposal endpoint; DismissAsync's exceptions
            // propagate to the endpoint exactly like the approve path's do, and the request is only
            // marked Rejected once the proposal is dismissed.
            if (request.ProposalId is { } rejectedProposalId)
            {
                await _proposalApprovalService.DismissAsync(rejectedProposalId, cancellationToken).ConfigureAwait(false);
            }

            request.Status = ToolApprovalRequestStatus.Rejected;
            request.DecidedByUserId = decidingUserId;
            request.DecidedAt = now;
            if (request.WorkflowStepRunId.HasValue) await _workflowContinuations!.AfterDecisionAsync(request, cancellationToken);
            await _requestStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new ToolApprovalDecisionResult(
                request.Id, ToolApprovalDecisionOutcome.Rejected, request.ProposalId,
                Message: "The action was rejected and will not run.");
        }

        // Approve.
        if (request.ProposalId is { } proposalId)
        {
            // High: route through the same policy-checked money path the approvals queue uses, so
            // an in-session approval and a queue approval take the identical authorisation + dispatch
            // path. ApproveAsync executes the handler synchronously and is terminal on failure for
            // money (§8.1); its exceptions propagate to the endpoint, which maps them like the
            // proposal-approval endpoint does. The request is only marked Approved on success.
            var detail = await _proposalApprovalService.GetByIdAsync(proposalId, cancellationToken).ConfigureAwait(false);
            if (detail is null)
            {
                return new ToolApprovalDecisionResult(
                    request.Id, ToolApprovalDecisionOutcome.NotFound, proposalId,
                    Message: "The linked proposal is no longer available.");
            }

            var authorization = _proposalApprovalPolicy.Authorize(
                new ApprovalActor(decidingUserId, request.TenantId),
                new ProposalAuthorizationContext(
                    detail.Id, request.TenantId, detail.ProposalType, detail.RiskTier,
                    OriginatingUserId: request.RequestingUserId));
            if (!authorization.IsAuthorized)
            {
                return new ToolApprovalDecisionResult(
                    request.Id, ToolApprovalDecisionOutcome.Forbidden, proposalId,
                    Message: authorization.Reason ?? "You are not authorized to decide this proposal.");
            }

            await _proposalApprovalService.ApproveAsync(proposalId, cancellationToken).ConfigureAwait(false);

            request.Status = ToolApprovalRequestStatus.Approved;
            request.DecidedByUserId = decidingUserId;
            request.DecidedAt = now;
            request.ConsumedAt = now; // High executes via the proposal, not an inline re-run.
            await _requestStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ToolApprovalDecisionResult(
                request.Id, ToolApprovalDecisionOutcome.Approved, proposalId,
                Message: "The action was approved and executed.");
        }

        // Medium: record the approval. The gate consumes it (args-hash bound, single-use) when the
        // agent re-invokes the tool — that is what actually runs the inner domain call.
        request.Status = ToolApprovalRequestStatus.Approved;
        request.DecidedByUserId = decidingUserId;
        request.DecidedAt = now;
        if (request.WorkflowStepRunId.HasValue) await _workflowContinuations!.AfterDecisionAsync(request, cancellationToken);
        await _requestStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ToolApprovalDecisionResult(
            request.Id, ToolApprovalDecisionOutcome.Approved, ProposalId: null,
            Message: request.WorkflowStepRunId.HasValue ? "The action was approved. The workflow will continue automatically."
                : "The action was approved. Ask the agent to proceed.");
    }

    private ToolApprovalRequest NewRequest(
        ToolGateContext context,
        ToolApprovalOptions options,
        string actionKind,
        string argumentsJson,
        string argsHash,
        Guid tenantId,
        Guid? requestingUserId,
        DateTime now)
    {
        var request = new ToolApprovalRequest
        {
            TenantId = tenantId,
            RequestingUserId = requestingUserId,
            AiRunId = _workflowContext?.Step?.AiRunId ?? _roomContext?.AiRunId,
            ThreadId = _roomContext?.RoomId?.ToString(),
            ToolName = context.ToolName,
            ArgumentsRedactedJson = argumentsJson,
            PreviewJson = context.Preview is null ? null : JsonSerializer.Serialize(context.Preview),
            ArgsHash = argsHash,
            RiskTier = options.Tier.ToString(),
            ActionKind = actionKind,
            RequestedAt = now,
        };
        if (_workflowContext?.Step is { } step && _workflowContext.Run is { } run)
        {
            request.WorkflowStepRunId = step.Id;
            request.RequestingUserId = run.InitiatingUserId;
            request.ThreadId = $"workflow:{step.Id:D}";
            step.ApprovalRequestId = request.Id;
        }
        return request;
    }

    private void StageWorkflowExpiry(ToolApprovalRequest request)
    {
        if (request.WorkflowStepRunId is null) return;
        if (_workflowContinuations is null || _workflowContext?.Run is not { } run)
            throw new InvalidOperationException("Workflow continuation is unavailable.");
        _workflowContinuations.AtExpiry(request, run);
    }
}
