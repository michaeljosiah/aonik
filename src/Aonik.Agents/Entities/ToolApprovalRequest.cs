using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// Lifecycle of a <see cref="ToolApprovalRequest"/> (Spec 032 §12). Persisted as a string
/// (see <c>ToolApprovalRequestConfiguration</c>) so adding values is a code-only change.
/// </summary>
public enum ToolApprovalRequestStatus
{
    /// <summary>Created by the gate, awaiting a human decision (Medium / High).</summary>
    Pending = 1,

    /// <summary>
    /// A decision approved the action. For Low this is the as-created state (auto-approved);
    /// for Medium/High a human approved it via <c>DecideAsync</c>. An Approved row is eligible to
    /// be consumed by the gate on the next invocation — once — to run the inner tool in-band.
    /// </summary>
    Approved = 2,

    /// <summary>A decision rejected the action. Terminal.</summary>
    Rejected = 3,

    /// <summary>The request passed its <see cref="ToolApprovalRequest.ExpiresAt"/> before a decision. Terminal.</summary>
    Expired = 4,
}

/// <summary>
/// Durable audit + correlation row for a single gated mutating-tool invocation (Spec 032 §7.5, §12).
/// Created for <em>every</em> classified mutation regardless of tier, so the invariant "every
/// mutation is classified and audited" holds without exception:
/// <list type="bullet">
///   <item><strong>Low</strong> — created already <see cref="ToolApprovalRequestStatus.Approved"/>
///   (and immediately consumed), recording the in-band run.</item>
///   <item><strong>Medium</strong> — created <see cref="ToolApprovalRequestStatus.Pending"/>; a
///   server-validated decision flips it to Approved/Rejected. The gate consumes a matching Approved
///   row (bound by <see cref="ArgsHash"/>) on the agent's resubmit to run the inner tool once.</item>
///   <item><strong>High</strong> — created Pending and linked to the durable
///   <see cref="ProposalId"/> the money call was marshalled into; the proposal pipeline is the
///   execution authority.</item>
/// </list>
/// It answers "what did this conversation ask for and who consented"; the linked <c>Proposal</c>
/// (High only) answers "what durable money operation is pending and what executed it".
/// </summary>
public class ToolApprovalRequest : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// The user whose session triggered the gate. For consumer flows the deciding user MUST equal
    /// this user (Spec 032 §12). Nullable because not every agent context has a resolvable user.
    /// </summary>
    public Guid? RequestingUserId { get; set; }

    /// <summary>Best-effort correlation to the originating chat thread (not always ambient at the gate).</summary>
    public string? ThreadId { get; set; }

    /// <summary>Best-effort correlation to the proposing agent.</summary>
    public Guid? AgentId { get; set; }

    /// <summary>Best-effort correlation to the AI run that issued the tool call.</summary>
    public Guid? AiRunId { get; set; }

    /// <summary>The gated tool's name (e.g. <c>finance_create_invoice</c>).</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Best-effort correlation to the model's tool-call id.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// The model-supplied arguments serialized to JSON for the audit trail. Named "redacted" to
    /// honour the Spec 032 §12 contract that cards/records carry summaries and material change
    /// details, never secrets — redaction of sensitive keys is layered on later.
    /// </summary>
    public string ArgumentsRedactedJson { get; set; } = string.Empty;

    /// <summary>
    /// Stable hash of the canonical arguments. A decision is bound to this hash: the gate only
    /// consumes an Approved row whose <see cref="ArgsHash"/> matches the re-invoked call, so a
    /// decision never authorises a call with changed arguments (Spec 032 §11 replay guard).
    /// </summary>
    public string ArgsHash { get; set; } = string.Empty;

    /// <summary>The tool's risk tier as a string ("Low" / "Medium" / "High").</summary>
    public string RiskTier { get; set; } = string.Empty;

    /// <summary>Short human label for the action, shown in the approval card and audit.</summary>
    public string ActionKind { get; set; } = string.Empty;

    public ToolApprovalRequestStatus Status { get; set; } = ToolApprovalRequestStatus.Pending;

    /// <summary>The user who decided (approved/rejected). Set by <c>DecideAsync</c>.</summary>
    public Guid? DecidedByUserId { get; set; }

    /// <summary>When the decision was recorded.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>When the gate created the request.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the request stops being decidable / consumable. A Medium request that is not decided
    /// and consumed before this bounds the replay window (Spec 032 §11 expiry).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When an Approved row was consumed to run the inner tool in-band. Non-null means it has
    /// already been used and cannot drive a second execution (single-use decision).
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>For High, the durable <c>Proposal</c> this request was marshalled into.</summary>
    public Guid? ProposalId { get; set; }
}
