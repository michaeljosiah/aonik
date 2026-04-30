namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Allowed values for <see cref="Workflow.State"/>. Stored as a short
/// string column so we don't need a database-level enum mapping; treat
/// these constants as the authoritative set.
/// </summary>
public static class WorkflowStates
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Draft = "Draft";
}
