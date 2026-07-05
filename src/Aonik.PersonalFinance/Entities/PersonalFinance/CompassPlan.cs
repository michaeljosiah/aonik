using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// A versioned AONIK Compass plan for a <see cref="Goal"/> (Spec 021 §2).
/// The plan is the only new required Compass entity in V1 because it needs an
/// explicit lifecycle, versioning, and linkage to the grounding inputs that
/// produced it (the <see cref="SnapshotId"/> and the <see cref="AiRunId"/>).
/// Anemic: all lifecycle logic lives in <c>ICompassPlanService</c>.
/// </summary>
public class CompassPlan : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid GoalId { get; set; }

    /// <summary>Monotonic version per goal — 1, 2, 3 … as plans are regenerated.</summary>
    public int Version { get; set; }

    /// <summary>"Active", "Superseded", "Completed", "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Structured plan payload (the planner sub-agent's JSON output).</summary>
    public string PlanJson { get; set; } = string.Empty;

    public DateTime HorizonStartUtc { get; set; }
    public DateTime HorizonEndUtc { get; set; }

    /// <summary>The <c>CustomerInsightSnapshot</c> the plan was grounded on, when one was available.</summary>
    public Guid? SnapshotId { get; set; }

    /// <summary>The <c>AiRun</c> that produced this plan (audit trail).</summary>
    public Guid? AiRunId { get; set; }

    /// <summary>When this plan was superseded, the plan that replaced it.</summary>
    public Guid? SupersededById { get; set; }
}
