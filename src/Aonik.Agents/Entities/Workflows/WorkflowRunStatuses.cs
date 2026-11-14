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
    public const string Queued = "Queued";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string WaitingForApproval = "WaitingForApproval";
    public const string WaitingForConnection = "WaitingForConnection";
    public const string NeedsAttention = "NeedsAttention";
}
