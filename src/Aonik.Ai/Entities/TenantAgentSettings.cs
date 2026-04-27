using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

/// <summary>
/// Per-tenant runtime configuration for the agent fleet. Today this only
/// carries the global kill-switch flag — when engaged, the AI Policies UI
/// renders the banner red and (once enforcement lands) every new agent run
/// short-circuits without invoking a model.
///
/// Singleton-per-tenant: at most one row per <see cref="ITenantScoped.TenantId"/>.
/// Read via <c>FirstOrDefaultAsync</c> with a default-value fallback for
/// tenants that have never engaged the switch.
/// </summary>
public class TenantAgentSettings : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// When true, every new agent run is rejected at the orchestrator
    /// boundary. Defaults false — the absence of a row is treated as
    /// "not engaged" by the read path.
    /// </summary>
    public bool KillSwitchEngaged { get; set; }

    /// <summary>UTC timestamp the kill switch was last engaged. Null when
    /// the switch is currently off.</summary>
    public DateTime? KillSwitchEngagedAt { get; set; }

    /// <summary>User who engaged the switch. Null when off.</summary>
    public Guid? KillSwitchEngagedByUserId { get; set; }
}
