namespace Aonik.Agents.Entities.Workflows;

/// <summary>
/// Allowed values for <see cref="WorkflowRun.Status"/>.
/// </summary>
public static class WorkflowRunStatuses
{
    public const string Running = "Running";
    public const string Success = "Success";
    public const string Held = "Held";
    public const string Failed = "Failed";
}
