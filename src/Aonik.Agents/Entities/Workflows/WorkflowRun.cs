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

    /// <summary>Ordered legacy node IDs or live step-run IDs; live details use the pinned version.</summary>
    public string SequenceJson { get; set; } = "[]";

    // Null metadata preserves the meaning of legacy, non-durable run history.
    public Guid? WorkflowVersionId { get; set; }
    public string? DefinitionHash { get; set; }
    public string Mode { get; set; } = "Legacy";
    public string? ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? InitiatingUserId { get; set; }
    public Guid? ConfiguredByUserId { get; set; }
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? InputHash { get; set; }
    public string? FactsJson { get; set; }
    public string? FactsHash { get; set; }
    public string? Outcome { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? PendingStepRunId { get; set; }
    public string? CheckpointId { get; set; }
    public string? FrameworkSessionId { get; set; }
    public int Continuation { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
}
