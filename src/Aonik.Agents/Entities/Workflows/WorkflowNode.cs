using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// One step in a workflow graph. The kind drives the visual treatment in
/// the editor and the per-kind parameter shape stored in
/// <see cref="ParamsJson"/>. Position (<see cref="X"/> / <see cref="Y"/>)
/// is world-space coordinates on the canvas grid.
/// </summary>
public class WorkflowNode : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    /// <summary>One of <see cref="WorkflowNodeKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Optional one-line summary shown under the label on the canvas.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Optional inspector-only notes.</summary>
    public string Notes { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// Per-kind parameters (tool name + JSON args, agent + task brief,
    /// decision expression + branch labels, etc.). Stored as raw JSON so
    /// kinds can evolve without schema churn.
    /// </summary>
    public string ParamsJson { get; set; } = "{}";
}
