using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services;

/// <summary>
/// Translates a Spec 032 approval result (returned by the <c>ApprovalGatedAIFunction</c> when a
/// mutating tool is gated and NOT executed in-band) into the machine-parseable AG-UI CUSTOM event
/// the client renders an approval card from. Shared by both stream paths
/// (<see cref="AguiStreamPipeline"/> and the playground endpoint) so the wire shape stays identical.
///
/// <para>
/// Before this, a gated Medium/High call reached the client only as (a) a <c>TOOL_CALL_RESULT</c>
/// whose <c>content</c> is the C# record's <c>ToString()</c> (not parseable), and (b) assistant
/// prose. Neither carries the durable <c>approvalRequestId</c>/<c>proposalId</c> the client needs to
/// route the user's decision to <c>POST /ai/tool-approvals/{id}/decide</c>. This emits a structured
/// CUSTOM event that does — and lets the stream set <c>requiresApproval</c> from the gate's result
/// rather than from the model happening to call the legacy <c>confirmAction</c> frontend tool.
/// </para>
/// </summary>
internal static class ToolApprovalStreamEvents
{
    /// <summary>CUSTOM event name for a Medium-tier in-session confirmation (carries an approval request id).</summary>
    public const string ApprovalRequiredEventName = "tool.approval.required";

    /// <summary>CUSTOM event name for a High-tier action marshalled into a durable proposal (carries a proposal id).</summary>
    public const string ApprovalQueuedEventName = "tool.approval.queued";

    /// <summary>
    /// Inspects a tool's function result for an approval signal. Returns whether the result means the
    /// action requires human approval (so the stream sets <c>requiresApproval</c>) and — when the
    /// signal is actionable (carries a durable id) — the CUSTOM event payload to write to the wire.
    /// </summary>
    /// <param name="functionResult">The raw <c>FunctionResultContent.Result</c> from the stream.</param>
    /// <param name="toolCallId">The tool call id, used by the client to correlate to the prior TOOL_CALL_ARGS.</param>
    public static ToolApprovalSignal Inspect(object? functionResult, string? toolCallId)
    {
        switch (functionResult)
        {
            case ToolApprovalRequiredResult required:
                // Only emit a card event when a durable request was persisted (actionable). The
                // fail-closed overload (no gate service / no resolvable tenant) carries no id — we
                // still flag requiresApproval so the assistant guidance is accurate, but the user has
                // nothing to route a decision to, so no card event is emitted.
                object? requiredEvent = required.ApprovalRequestId is { } requestId
                    ? new
                    {
                        type = "CUSTOM",
                        name = ApprovalRequiredEventName,
                        value = new
                        {
                            approvalRequestId = requestId,
                            toolCallId,
                            tool = required.Tool,
                            tier = required.Tier,
                            actionKind = required.ActionKind,
                            status = required.Status,
                        },
                    }
                    : null;

                return new ToolApprovalSignal(
                    RequiresApproval: true,
                    CustomEvent: requiredEvent,
                    ApprovalKey: required.ApprovalRequestId?.ToString());

            case ToolApprovalQueuedResult queued:
                return new ToolApprovalSignal(
                    RequiresApproval: true,
                    CustomEvent: new
                    {
                        type = "CUSTOM",
                        name = ApprovalQueuedEventName,
                        value = new
                        {
                            proposalId = queued.ProposalId,
                            // The decide target: the in-session card routes to /ai/tool-approvals/{id}/decide
                            // (not the bare proposal endpoint), so the request is resolved with the proposal.
                            approvalRequestId = queued.ApprovalRequestId,
                            toolCallId,
                            tool = queued.Tool,
                            tier = queued.Tier,
                            actionKind = queued.ActionKind,
                            status = queued.Status,
                        },
                    },
                    ApprovalKey: queued.ProposalId.ToString());

            default:
                return ToolApprovalSignal.None;
        }
    }
}

/// <summary>
/// Outcome of inspecting a tool result for a Spec 032 approval signal.
/// </summary>
/// <param name="RequiresApproval">
/// True when the result is a gated-but-not-executed approval result — the stream's authoritative,
/// gate-driven source for the <c>requiresApproval</c> flag.
/// </param>
/// <param name="CustomEvent">
/// The CUSTOM event payload to write to the wire, or null when there is nothing actionable to
/// render (e.g. a fail-closed refusal that persisted no durable request).
/// </param>
/// <param name="ApprovalKey">
/// Stable identity of the approval (the durable approvalRequestId or proposalId), used by the
/// pipeline to de-duplicate a top-level call that is both inspected inline AND drained from the
/// notifier. Null when the signal carries no actionable id.
/// </param>
internal readonly record struct ToolApprovalSignal(bool RequiresApproval, object? CustomEvent, string? ApprovalKey = null)
{
    /// <summary>No approval signal — the result was an ordinary tool result.</summary>
    public static ToolApprovalSignal None { get; } = new(false, null, null);
}
