namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Request-scoped collector that bridges a gated mutation to the active client's stream
/// (Spec 032 §7.7 transport bridge). The <c>ApprovalGatedAIFunction</c> decorator records the
/// structured approval result (<c>ToolApprovalRequiredResult</c> / <c>ToolApprovalQueuedResult</c>)
/// here when it blocks a Medium/High tool; the AG-UI stream pipeline drains it after the run and
/// emits the <c>tool.approval.required</c> / <c>tool.approval.queued</c> CUSTOM event.
/// <para>
/// Why this exists: the pipeline can only inspect tool results on the <em>top-level</em> stream.
/// When a gated tool runs <em>nested</em> inside a sub-agent (the agent-as-tool orchestration the
/// master orchestrator uses), its structured result is consumed one level down and collapsed into
/// the sub-agent's text reply, so the top-level inspection never sees it and no approval card is
/// emitted. The decorator runs in the same request DI scope regardless of nesting depth, so
/// recording here — and draining once at the end of the run — surfaces the card no matter how deep
/// the gated call was. Scoped registration keeps one buffer per AG-UI request.
/// </para>
/// </summary>
public interface IToolApprovalStreamNotifier
{
    /// <summary>
    /// Record an approval result produced by a gated tool that did NOT run in-band — either a
    /// <c>ToolApprovalRequiredResult</c> (Medium, pending) or a <c>ToolApprovalQueuedResult</c>
    /// (High, queued). Null is ignored. Safe to call from concurrent tool executions.
    /// </summary>
    void Record(object approvalResult);

    /// <summary>
    /// Return every recorded result and clear the buffer. Called once by the stream pipeline at
    /// the end of a run; the pipeline de-duplicates against ids it already emitted inline.
    /// </summary>
    IReadOnlyList<object> Drain();
}
