using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Goal : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FundingAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public decimal ProgressAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    // ── AONIK Compass programme fields (Spec 021 §1) ──────────────
    // Nullable so existing goal rows remain valid; a goal becomes a
    // "Compass programme" once these are populated via the goal service.

    /// <summary>"cashflow", "savings", "debt_reduction", "purchase".</summary>
    public string? GoalType { get; set; }

    /// <summary>Short strategy summary the planner and UI can surface.</summary>
    public string? Strategy { get; set; }

    /// <summary>"conservative", "moderate", "aggressive".</summary>
    public string? RiskAppetite { get; set; }

    /// <summary>Relative priority across the user's goals (lower number = higher priority).</summary>
    public int? Priority { get; set; }

    /// <summary>JSON array of milestone items.</summary>
    public string? MilestonesJson { get; set; }

    /// <summary>Soft FK to the current active <see cref="CompassPlan"/> for this goal.</summary>
    public Guid? ActivePlanId { get; set; }
}
