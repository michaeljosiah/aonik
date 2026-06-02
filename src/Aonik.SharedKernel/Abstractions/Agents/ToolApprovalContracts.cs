namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Risk tier of a mutating agent tool, per
/// <see href="../../docs/specifications/032.tiered-ai-mutation-approval.html">Spec 032</see>.
/// Read-only tools are not assigned a tier (see <see cref="ToolClassification.ReadOnly"/>).
/// </summary>
public enum ToolApprovalTier
{
    /// <summary>Reversible personal-state write. Audited; runs in-band.</summary>
    Low,

    /// <summary>
    /// Everyday domain write (e.g. create invoice). Requires an explicit, server-validated
    /// confirmation before it may run: the gate persists a Pending <c>ToolApprovalRequest</c> and
    /// refuses the call; once the user approves (via <c>DecideAsync</c>) and the agent re-invokes,
    /// the gate consumes the approval — bound by args-hash — and the inner tool runs in-band once.
    /// </summary>
    Medium,

    /// <summary>
    /// Money movement, ledger posting, or partner call (e.g. capture payment). Never runs
    /// in-band — it is marshalled into a durable <c>Proposal</c> and executed only after
    /// approval. (The durable-proposal execution path is deferred from the focused slice.)
    /// </summary>
    High,
}

/// <summary>
/// Approval metadata attached to a mutating tool. Carries the risk <see cref="Tier"/> and,
/// for High tools, the durable <see cref="ProposalType"/> the mutation will be marshalled
/// into once the deferred proposal-execution path lands.
/// </summary>
/// <param name="Tier">The tool's risk tier.</param>
/// <param name="ActionKind">A short human label for the action, used in audit + refusal messages.</param>
/// <param name="ProposalType">For High tools, the <c>Proposal.ProposalType</c> the action maps to (e.g. "Finance.CapturePayment").</param>
public sealed record ToolApprovalOptions(
    ToolApprovalTier Tier,
    string? ActionKind = null,
    string? ProposalType = null);

/// <summary>
/// The classification of a single agent tool: either read-only (safe, passes through the
/// gate unchanged) or mutating (must be wrapped so it cannot execute ungated). Mutating
/// classifications carry their <see cref="Options"/>.
/// </summary>
public sealed class ToolClassification
{
    private ToolClassification(bool isReadOnly, ToolApprovalOptions? options)
    {
        IsReadOnly = isReadOnly;
        Options = options;
    }

    /// <summary>True for a read-only tool — the gate passes it through unchanged.</summary>
    public bool IsReadOnly { get; }

    /// <summary>True for a mutating tool — the gate wraps it in the approval decorator.</summary>
    public bool IsMutating => !IsReadOnly;

    /// <summary>Approval options. Non-null when <see cref="IsMutating"/>; null when read-only.</summary>
    public ToolApprovalOptions? Options { get; }

    /// <summary>A read-only classification (passthrough).</summary>
    public static ToolClassification ReadOnly { get; } = new(isReadOnly: true, options: null);

    /// <summary>A mutating classification carrying the given approval options.</summary>
    public static ToolClassification Mutating(ToolApprovalOptions options) =>
        new(isReadOnly: false, options ?? throw new ArgumentNullException(nameof(options)));
}

/// <summary>
/// Structured result returned by the approval gate when a mutating tool is gated and NOT
/// executed in-band. It tells the model the action requires human approval and that no
/// change was made, so the agent reports a pending action rather than a success.
/// </summary>
/// <remarks>
/// Medium returns this carrying an <see cref="ApprovalRequestId"/>: a durable
/// <c>ToolApprovalRequest</c> was created in the Pending state, and the agent should surface it for
/// the user to approve, then re-invoke. The parameterless-request <see cref="For(string, ToolApprovalOptions)"/>
/// overload is the fail-closed result used when no gate service is available (no request was
/// persisted). Low-tier tools are audited and run in-band, so they never return this.
/// </remarks>
public sealed record ToolApprovalRequiredResult(
    string Tool,
    string Tier,
    string ActionKind,
    bool Executed,
    string Status,
    string Message,
    Guid? ApprovalRequestId = null)
{
    /// <summary>The <see cref="Status"/> value used for a gated-but-not-executed action.</summary>
    public const string RequiresApprovalStatus = "RequiresApproval";

    /// <summary>Builds a fail-closed refusal result for the given tool + options (no persisted request).</summary>
    public static ToolApprovalRequiredResult For(string tool, ToolApprovalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var action = options.ActionKind ?? tool;
        return new ToolApprovalRequiredResult(
            Tool: tool,
            Tier: options.Tier.ToString(),
            ActionKind: action,
            Executed: false,
            Status: RequiresApprovalStatus,
            Message:
                $"The '{action}' action is a {options.Tier}-risk mutation and was NOT executed. " +
                "It requires explicit human approval before it can run. No changes were made. " +
                "Tell the user the action is pending their approval — do not claim it succeeded.");
    }

    /// <summary>
    /// Builds a requires-approval result for a Medium action that has been persisted as a Pending
    /// <c>ToolApprovalRequest</c>. Carries the <paramref name="approvalRequestId"/> so the
    /// presentation layer can route the user's decision to <c>DecideAsync</c>.
    /// </summary>
    public static ToolApprovalRequiredResult For(string tool, ToolApprovalOptions options, Guid approvalRequestId)
    {
        ArgumentNullException.ThrowIfNull(options);
        var action = options.ActionKind ?? tool;
        return new ToolApprovalRequiredResult(
            Tool: tool,
            Tier: options.Tier.ToString(),
            ActionKind: action,
            Executed: false,
            Status: RequiresApprovalStatus,
            Message:
                $"The '{action}' action is a {options.Tier}-risk mutation and was NOT executed. " +
                $"It is prepared and awaiting the user's approval (request {approvalRequestId}). No changes were made. " +
                "Ask the user to approve it; once they do, retry the same action. Do not claim it succeeded.",
            ApprovalRequestId: approvalRequestId);
    }
}

/// <summary>
/// Structured result returned to the model when a High-tier tool is marshalled into a durable
/// <c>Proposal</c> (Spec 032 §7.4). The inner domain call was NOT invoked — the matching
/// <c>IProposalHandler</c> will execute it only after the proposal is approved. Carries the
/// durable <see cref="ProposalId"/> so the agent can reference the pending action.
/// </summary>
public sealed record ToolApprovalQueuedResult(
    string Tool,
    string Tier,
    string ActionKind,
    bool Executed,
    string Status,
    Guid ProposalId,
    string Message)
{
    /// <summary>The <see cref="Status"/> value used for a queued-for-approval action.</summary>
    public const string QueuedStatus = "Queued";

    /// <summary>Builds a queued result for the given tool, options, and created proposal.</summary>
    public static ToolApprovalQueuedResult For(
        string tool,
        ToolApprovalOptions options,
        Guid proposalId,
        string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var action = options.ActionKind ?? tool;
        return new ToolApprovalQueuedResult(
            Tool: tool,
            Tier: options.Tier.ToString(),
            ActionKind: action,
            Executed: false,
            Status: QueuedStatus,
            ProposalId: proposalId,
            Message:
                $"The '{action}' action is a {options.Tier}-risk money movement and was NOT executed. " +
                $"It has been queued as proposal {proposalId} and will run only after a human approves it. " +
                "Tell the user the action is prepared and awaiting their approval — do not claim it succeeded.");
    }
}

/// <summary>How <see cref="IToolApprovalService.GateAsync"/> routed a gated mutating tool.</summary>
public enum ToolGateDecision
{
    /// <summary>Run the inner tool in-band now. Low (auto-approved), or Medium when a matching
    /// server-validated approval already exists and was consumed for this call. The caller invokes
    /// the domain call.</summary>
    ApprovedInline,

    /// <summary>A durable <c>Proposal</c> was created (High). The inner tool is never invoked in-band.</summary>
    Queued,

    /// <summary>
    /// Medium: no approved decision exists yet, so a <c>ToolApprovalRequest</c> was created in the
    /// Pending state. The inner tool must NOT run; the caller surfaces an approval card carrying the
    /// <see cref="ToolGateOutcome.ApprovalRequestId"/>, and the agent re-invokes the tool after the
    /// user decides (the gate then consumes the approval and returns <see cref="ApprovedInline"/>).
    /// </summary>
    PendingApproval,

    /// <summary>The action was refused (rejected / expired / failed to gate). The inner tool must not run.</summary>
    Refused,
}

/// <summary>
/// Input to <see cref="IToolApprovalService.GateAsync"/> for a single gated invocation: the
/// tool's name, its approval <see cref="Options"/> (carrying tier + High <c>ProposalType</c>),
/// and the model-supplied <see cref="Arguments"/> that become the proposal payload.
/// </summary>
public sealed record ToolGateContext(
    string ToolName,
    ToolApprovalOptions Options,
    IDictionary<string, object?> Arguments);

/// <summary>Uniform outcome of <see cref="IToolApprovalService.GateAsync"/>.</summary>
/// <param name="Decision">How the call was routed.</param>
/// <param name="ApprovalRequestId">The durable <c>ToolApprovalRequest</c> id created for this call.
/// Null only on the pre-persist fail-closed paths (no resolvable tenant / misconfigured tool) where
/// no tenant-scoped row could be written.</param>
/// <param name="ProposalId">The durable proposal id — set only when <see cref="Decision"/> is <see cref="ToolGateDecision.Queued"/>.</param>
/// <param name="Summary">Short human label for the action (used in the queued / requires-approval message).</param>
/// <param name="Reason">Refusal reason — set only when <see cref="Decision"/> is <see cref="ToolGateDecision.Refused"/>.</param>
public sealed record ToolGateOutcome(
    ToolGateDecision Decision,
    Guid? ApprovalRequestId = null,
    Guid? ProposalId = null,
    string? Summary = null,
    string? Reason = null);

/// <summary>Whether a <see cref="IToolApprovalService.DecideAsync"/> caller is approving or rejecting.</summary>
public enum ToolApprovalDecisionType
{
    /// <summary>Approve the pending request so the gate may run the inner tool on the agent's resubmit.</summary>
    Approve,

    /// <summary>Reject the pending request; the inner tool will never run for it.</summary>
    Reject,
}

/// <summary>
/// Input to <see cref="IToolApprovalService.DecideAsync"/>: the decision and an optional
/// human-supplied reason (recorded on the request / proposal).
/// </summary>
public sealed record ToolApprovalDecisionInput(
    ToolApprovalDecisionType Decision,
    string? Reason = null);

/// <summary>How <see cref="IToolApprovalService.DecideAsync"/> resolved a decision attempt.</summary>
public enum ToolApprovalDecisionOutcome
{
    /// <summary>The request was approved and recorded. The gate will run the inner tool on resubmit.</summary>
    Approved,

    /// <summary>The request was rejected and recorded. The inner tool will never run for it.</summary>
    Rejected,

    /// <summary>No request with that id is visible to the caller (wrong id or cross-tenant). Map to 404.</summary>
    NotFound,

    /// <summary>The caller is not allowed to decide this request (identity / tenant / policy). Map to 403.</summary>
    Forbidden,

    /// <summary>The request had already expired before the decision. Map to 409.</summary>
    Expired,

    /// <summary>The request was already decided (Approved/Rejected) and cannot be decided again. Map to 409.</summary>
    AlreadyDecided,
}

/// <summary>Uniform result of <see cref="IToolApprovalService.DecideAsync"/>.</summary>
/// <param name="ApprovalRequestId">The request that was decided (or attempted).</param>
/// <param name="Outcome">How the attempt resolved.</param>
/// <param name="ProposalId">For an approved High request, the durable proposal that executed (or was queued).</param>
/// <param name="Message">Human-readable explanation, surfaced to the deciding client.</param>
public sealed record ToolApprovalDecisionResult(
    Guid ApprovalRequestId,
    ToolApprovalDecisionOutcome Outcome,
    Guid? ProposalId,
    string? Message);

/// <summary>
/// Canonical hash of model-supplied tool arguments, used to bind an approval decision to the exact
/// arguments it was shown (Spec 032 §11 replay guard). The gate consumes an Approved
/// <c>ToolApprovalRequest</c> only when the re-invoked call's hash matches, so a decision never
/// authorises a call with changed arguments.
/// </summary>
public static class ToolArgumentsHash
{
    /// <summary>
    /// Computes a stable SHA-256 hex hash over <paramref name="arguments"/>. Keys are ordered so
    /// the hash is independent of enumeration order; values are serialized to their JSON form, so
    /// the same logical arguments produce the same hash across the create and the resubmit call.
    /// </summary>
    public static string Compute(IDictionary<string, object?>? arguments)
    {
        var ordered = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        if (arguments is not null)
        {
            foreach (var kvp in arguments)
            {
                ordered[kvp.Key] = kvp.Value;
            }
        }

        var json = System.Text.Json.JsonSerializer.Serialize(ordered);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>
/// Server-side front door that routes a gated mutating tool by tier (Spec 032 §7.5). The
/// <c>ApprovalGatedAIFunction</c> decorator delegates to this so the routing — and the
/// identity / tenant / expiry / replay enforcement — lives in one testable place rather than in
/// the decorator or any transport adapter.
/// <para>
/// <see cref="GateAsync"/> persists a durable <c>ToolApprovalRequest</c> for every gated call and
/// routes by tier: Low is auto-approved and run in-band; Medium creates a Pending request (or
/// consumes an already-approved one, bound by args-hash) so the agent re-invokes after the user
/// decides; High marshals the call into a durable <c>Proposal</c> and returns
/// <see cref="ToolGateDecision.Queued"/> so the money call never runs in-band.
/// </para>
/// <para>
/// <see cref="DecideAsync"/> is the single, transport-neutral decision authority: a decision
/// arriving over any transport is validated here for identity, tenant, freshness/expiry, and
/// status before it has any effect. For a High request it routes through the policy-checked
/// proposal-approval path; the inner domain call is never reached from a transport.
/// </para>
/// </summary>
public interface IToolApprovalService
{
    /// <summary>
    /// Routes a gated invocation by tier and persists a <c>ToolApprovalRequest</c>. Medium returns
    /// <see cref="ToolGateDecision.PendingApproval"/> until a matching approval is consumed; High
    /// persists a durable proposal and returns <see cref="ToolGateDecision.Queued"/> with its id.
    /// </summary>
    Task<ToolGateOutcome> GateAsync(ToolGateContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a server-validated decision for a pending request (Medium), or routes a High
    /// request's decision through the policy-checked proposal-approval path. Enforces identity,
    /// tenant, freshness/expiry, and single-use status. Never runs the inner tool itself: an
    /// approved Medium request is consumed by <see cref="GateAsync"/> on the agent's resubmit.
    /// </summary>
    Task<ToolApprovalDecisionResult> DecideAsync(
        Guid approvalRequestId,
        ToolApprovalDecisionInput decision,
        CancellationToken cancellationToken = default);
}

/// <summary>One audit record for a gated mutating-tool invocation.</summary>
/// <param name="Tool">The tool name.</param>
/// <param name="Tier">The tool's risk tier.</param>
/// <param name="Executed">True if the inner domain call ran (Low); false if it was blocked (Medium/High).</param>
/// <param name="Outcome">A short machine-readable outcome code.</param>
public sealed record ToolApprovalAuditEntry(
    string Tool,
    ToolApprovalTier Tier,
    bool Executed,
    string Outcome);

/// <summary>
/// Sink for tool-approval audit records. Honours the Spec 032 invariant that <em>every</em>
/// classified mutation is audited — whether it ran in-band (Low) or was blocked (Medium/High).
/// Implementations must not throw.
/// </summary>
public interface IToolApprovalAuditSink
{
    /// <summary>Record a gated-tool invocation outcome.</summary>
    void Record(ToolApprovalAuditEntry entry);
}

/// <summary>
/// Thrown at agent-build time when a mutating-looking tool reaches the approval gate without
/// an explicit classification. Fail-closed: an unclassified mutation must never reach the
/// model. Resolve by adding the tool to its module's <see cref="IToolApprovalManifest"/>.
/// </summary>
public sealed class ToolNotClassifiedException : Exception
{
    public ToolNotClassifiedException(string toolName)
        : base($"Agent tool '{toolName}' looks like a mutation but has no approval classification. " +
               "Add it to the owning module's IToolApprovalManifest with a ToolApprovalTier " +
               "(Spec 032 fail-closed tool approval gate). Read-only tools need no entry.")
    {
        ToolName = toolName;
    }

    /// <summary>The unclassified tool's name.</summary>
    public string ToolName { get; }
}

/// <summary>
/// Name-based heuristic that flags a tool name as "mutating-looking". It is deliberately NOT
/// the runtime authority (the per-module <see cref="IToolApprovalManifest"/> is). It serves
/// two jobs only:
/// <list type="bullet">
///   <item>the gate's fail-closed default — an unclassified mutating-looking tool throws;</item>
///   <item>a lint trip-wire test — asserts no mutation-verb tool is classified read-only.</item>
/// </list>
/// Widened per Spec 032 §7.2 beyond the legacy verb set so update/delete/apply/pay/etc. trip it.
/// </summary>
public static class MutatingToolNameHeuristic
{
    private static readonly string[] MutationVerbs =
    {
        // Legacy set (AgentConfigurationService.IsMutatingToolName)
        "_create_", "_archive_", "_cancel_", "_issue_", "_mark_", "_capture_",
        // Spec 032 §7.2 widened set
        "_update_", "_delete_", "_apply_", "_pay_", "_transfer_", "_payout_",
        "_refund_", "_set_", "_override_", "_sync_",
        // Additional mutation verbs present in the current tool surface
        "_add_", "_remove_", "_post_",
    };

    /// <summary>
    /// True if <paramref name="toolName"/> contains a known mutation verb. The name is padded
    /// with underscores so a verb at the start or end of the name still matches.
    /// </summary>
    public static bool LooksMutating(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName))
        {
            return false;
        }

        var padded = "_" + toolName + "_";
        foreach (var verb in MutationVerbs)
        {
            if (padded.Contains(verb, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
