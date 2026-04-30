using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Pinned annotation on the workflow canvas — sticky-note style. Lives in
/// world-space so it tracks the same coordinates as the nodes when the
/// canvas pans.
/// </summary>
public class WorkflowComment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
