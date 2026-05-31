using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services;

/// <summary>
/// Server-side front door (Spec 032 §7.5) the <c>ApprovalGatedAIFunction</c> decorator delegates
/// to so tier routing lives in one testable place. In the focused High-path slice only the High
/// branch is live: it marshals the model-supplied arguments into a durable <c>Proposal</c>
/// (Status = Proposed, RiskTier = "High") via <see cref="IAgentProposalStore"/> and returns
/// <see cref="ToolGateDecision.Queued"/> with the new proposal id. The inner money call is
/// <strong>never</strong> invoked here — the matching <c>IProposalHandler</c> is the only path
/// that reaches it, and only after a human approves (Spec 030 dispatcher).
/// <para>
/// Low and Medium are handled in-band by the decorator itself, so calling
/// <see cref="GateAsync"/> for them just returns <see cref="ToolGateDecision.ApprovedInline"/>.
/// </para>
/// <para>
/// Fail-closed: any condition that prevents creating a proper tenant-scoped proposal — a High
/// tool with no <c>ProposalType</c>, or no resolvable tenant — returns
/// <see cref="ToolGateDecision.Refused"/> rather than letting the money call run ungated. The
/// decorator turns any non-Queued outcome into a requires-approval refusal.
/// </para>
/// </summary>
internal sealed class ToolApprovalService : IToolApprovalService
{
    private const string HighRiskTier = "High";

    private readonly IAgentProposalStore _proposalStore;
    private readonly ITenantProvider _tenantProvider;

    public ToolApprovalService(
        IAgentProposalStore proposalStore,
        ITenantProvider tenantProvider)
    {
        _proposalStore = proposalStore ?? throw new ArgumentNullException(nameof(proposalStore));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }

    public async Task<ToolGateOutcome> GateAsync(
        ToolGateContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options;
        var actionKind = options.ActionKind ?? context.ToolName;

        // Low / Medium are not marshalled here — the decorator runs Low in-band and refuses
        // Medium (the in-band confirm path is deferred). Report ApprovedInline so the contract
        // is uniform regardless of which tier reaches this router.
        if (options.Tier != ToolApprovalTier.High)
        {
            return new ToolGateOutcome(
                ToolGateDecision.ApprovedInline, ProposalId: null, Summary: actionKind, Reason: null);
        }

        // A High tool must declare the durable proposal type it maps to. Missing = misconfigured;
        // fail closed rather than execute the money call.
        if (string.IsNullOrWhiteSpace(options.ProposalType))
        {
            return new ToolGateOutcome(
                ToolGateDecision.Refused,
                ProposalId: null,
                Summary: actionKind,
                Reason: $"High-tier tool '{context.ToolName}' has no ProposalType, so it cannot be " +
                        "marshalled into a durable proposal.");
        }

        // No resolvable tenant means we cannot create a tenant-scoped proposal. Fail closed.
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return new ToolGateOutcome(
                ToolGateDecision.Refused,
                ProposalId: null,
                Summary: actionKind,
                Reason: $"No current tenant is available to scope the proposal for '{context.ToolName}'.");
        }

        // Snapshot the model-supplied arguments as the proposal payload. The IProposalHandler
        // reads these back by the original tool parameter names, so keys are preserved verbatim
        // (no naming policy) and the values (often JsonElement) serialize to their JSON form.
        var payload = new Dictionary<string, object?>(context.Arguments);
        var payloadJson = JsonSerializer.Serialize(payload);

        var proposalId = Guid.NewGuid();
        var request = new AgentProposalCreateRequest(
            Id: proposalId,
            TenantId: tenantId,
            ProposalType: options.ProposalType!,
            ProposedByAgentId: Guid.Empty,
            AiRunId: null,
            ImpactSummary: actionKind,
            RiskTier: HighRiskTier,
            PayloadJson: payloadJson);

        await _proposalStore
            .CreateManyAsync(new[] { request }, cancellationToken)
            .ConfigureAwait(false);

        return new ToolGateOutcome(ToolGateDecision.Queued, proposalId, Summary: actionKind, Reason: null);
    }
}
