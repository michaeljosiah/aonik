using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// History entry for a workflow. Captured each time the editor saves a
/// material change. The most-recent row matches the workflow's current
/// <see cref="Workflow.Version"/> tag.
/// </summary>
public class WorkflowVersion : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    /// <summary>Display tag, e.g. "v1.4". Unique per workflow.</summary>
    public string Tag { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Display name of the author at the time of the change.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Hex color for the author's avatar dot.</summary>
    public string AuthorColor { get; set; } = string.Empty;
}
