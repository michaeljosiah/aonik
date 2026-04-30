using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Reusable, agent-runnable procedure. A workflow is an ordered sequence of
/// typed steps (<see cref="WorkflowNode"/>) connected by directed edges
/// (<see cref="WorkflowEdge"/>). The same workflow can be wired to many
/// triggers and many agents.
///
/// Owner is referenced as an opaque <see cref="OwnerAgentId"/> rather than
/// a navigation property because the Agents module's read paths frequently
/// load workflows without the owning agent eagerly attached.
/// </summary>
public class Workflow : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable, URL-safe identifier (e.g. "match_and_apply"). Unique per tenant.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>FK to <see cref="Agent"/>. Null only during partially-constructed states.</summary>
    public Guid? OwnerAgentId { get; set; }

    /// <summary>Cached owner colour for display rails. Hex string, e.g. "#eb5c37".</summary>
    public string OwnerColor { get; set; } = string.Empty;

    /// <summary>JSON array of contributor agent ids (Guids serialised as strings).</summary>
    public string ContributorsJson { get; set; } = "[]";

    /// <summary>One of <see cref="WorkflowStates"/>.</summary>
    public string State { get; set; } = WorkflowStates.Draft;

    public string Version { get; set; } = "v0.1";

    public bool AutoRetry { get; set; }

    /// <summary>
    /// Computed/cached count of triggers wired to this workflow. The trigger
    /// network lives in another part of the system; we cache the count so
    /// list pages don't fan out to count rows on every request.
    /// </summary>
    public int TriggerCount { get; set; }
}
