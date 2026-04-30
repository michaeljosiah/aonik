using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// One execution of a workflow.
///
/// <see cref="SequenceJson"/> records the ordered list of node ids that
/// the run actually visited, so the editor's trace replay can step through
/// the path that fired (which can differ from the canonical graph if
/// branches were taken).
/// </summary>
public class WorkflowRun : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>One of <see cref="WorkflowRunStatuses"/>.</summary>
    public string Status { get; set; } = WorkflowRunStatuses.Running;

    /// <summary>Wall-clock duration in milliseconds. 0 while still running.</summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Free-form description of who/what triggered the run, e.g.
    /// "auto · banking.transaction.received" or "manual · maria@aonik.dev".
    /// </summary>
    public string StartedBy { get; set; } = string.Empty;

    /// <summary>JSON array of node ids visited, in order.</summary>
    public string SequenceJson { get; set; } = "[]";
}
