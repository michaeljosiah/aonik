using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Directed edge between two <see cref="WorkflowNode"/>s.
///
/// <see cref="FromIndex"/> selects which output port of the source node
/// the edge originates from — required for decision (0=yes / 1=no) and
/// loop (0=body / 1=done) nodes. Single-output kinds always use index 0.
/// </summary>
public class WorkflowEdge : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid FromNodeId { get; set; }

    public Guid ToNodeId { get; set; }

    public int FromIndex { get; set; }

    /// <summary>Optional edge label (e.g. "yes" / "no" / "body" / "done").</summary>
    public string Label { get; set; } = string.Empty;
}
