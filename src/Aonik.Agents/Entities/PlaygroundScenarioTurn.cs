using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A single message turn within a <see cref="PlaygroundScenario"/>.
/// Persists the role and content to enable full conversation replay
/// when a scenario is loaded into the playground.
/// </summary>
public class PlaygroundScenarioTurn : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>FK to the parent scenario.</summary>
    public Guid PlaygroundScenarioId { get; set; }

    /// <summary>Message role: "user", "assistant", or "system".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The text content of the message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Ordering within the scenario (monotonically increasing).</summary>
    public int SortOrder { get; set; }

    /// <summary>Navigation property to the parent scenario.</summary>
    public PlaygroundScenario PlaygroundScenario { get; set; } = null!;
}
