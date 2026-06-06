using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Tasks;

/// <summary>
/// The durable, tenant-scoped task primitive (Spec 034): an objective, about a
/// subject, due on a schedule, that fires an <em>action</em> when due. Anemic
/// data container — all behaviour lives in <c>WorkItemService</c> and
/// <c>WorkItemDispatcher</c>. The CLR type is named <c>WorkItem</c> (not
/// <c>Task</c>) to avoid colliding with <see cref="System.Threading.Tasks.Task"/>;
/// the product/API vocabulary remains "task".
/// </summary>
public class WorkItem : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    // What & why
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Kind { get; set; } = string.Empty;        // Reminder | ScheduledAction | AgentAssignment

    // About (polymorphic soft reference — NOT a cross-module FK)
    public string? SubjectType { get; set; }                // "Bill" | "Order" | "Party" | ...
    public Guid? SubjectId { get; set; }

    // Who acts
    public string AssigneeType { get; set; } = string.Empty; // System | User | Agent
    public Guid? AssigneeId { get; set; }                    // userId, or null for System
    public string? AssigneeKey { get; set; }                 // agent descriptor name when Agent

    // What happens when due — the extensibility seam
    public string ActionType { get; set; } = string.Empty;   // "notify_user" | "create_payment_proposal" | "run_agent" | ...
    public string ActionPayloadJson { get; set; } = "{}";    // handler-specific arguments

    // When
    public string ScheduleType { get; set; } = string.Empty; // OneOff | Recurring
    public DateTime? NextRunAtUtc { get; set; }              // null once terminal
    public string? RecurrenceCron { get; set; }              // Quartz/cron expression when Recurring
    public string? Timezone { get; set; }                    // IANA id for cron evaluation
    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public int RunCount { get; set; }
    public int? MaxRuns { get; set; }

    // Lifecycle
    public string Status { get; set; } = string.Empty;       // Scheduled | InProgress | Completed | Cancelled | Failed | Paused
    public int Priority { get; set; }
    public string SourceModule { get; set; } = string.Empty; // origin: "PersonalFinance", "Platform", ...
    public string? CorrelationId { get; set; }

    // Clustering safety (claim-before-execute lease — mirrors OutboxMessage)
    public DateTime? LeasedUntilUtc { get; set; }
    public string? LeasedBy { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
