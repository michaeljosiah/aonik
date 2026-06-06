using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Tasks;

/// <summary>
/// One execution occurrence of a <see cref="WorkItem"/> (Spec 034) — the audit
/// trail and the idempotency anchor for recurring items. Every dispatch writes a
/// run row; <c>(WorkItemId, ScheduledForUtc)</c> is unique, so the dispatcher
/// will not create two runs for the same occurrence even if two workers race or
/// the heartbeat double-fires. Mirrors <c>WorkflowRun</c> / <c>AiRun</c>.
/// </summary>
public class WorkItemRun : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid WorkItemId { get; set; }
    public DateTime ScheduledForUtc { get; set; }            // the occurrence instant this run satisfies
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Outcome { get; set; } = string.Empty;      // Succeeded | Failed | Skipped | Proposed (empty while in-flight)
    public string? ResultJson { get; set; }                  // handler result summary
    public string? Error { get; set; }
    public Guid? AiRunId { get; set; }                       // set when the action invoked AI
    public Guid? ProposalId { get; set; }                    // set when the action raised a Proposal
}
