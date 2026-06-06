namespace Aonik.SharedKernel.Abstractions.Tasks;

/// <summary>The shape of a task — what the system owner/customer/module is declaring.</summary>
public static class TaskKinds
{
    public const string Reminder = "Reminder";
    public const string ScheduledAction = "ScheduledAction";
    public const string AgentAssignment = "AgentAssignment";
}

/// <summary>Who acts when a task is due.</summary>
public static class TaskAssigneeTypes
{
    public const string System = "System";
    public const string User = "User";
    public const string Agent = "Agent";
}

/// <summary>Whether a task fires once or on a cadence.</summary>
public static class TaskScheduleTypes
{
    public const string OneOff = "OneOff";
    public const string Recurring = "Recurring";
}

/// <summary>Lifecycle states a task moves through.</summary>
public static class TaskStatuses
{
    public const string Scheduled = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
    public const string Paused = "Paused";
}

/// <summary>
/// Well-known <c>ActionType</c> keys. Each maps to a keyed <see cref="ITaskActionHandler"/>
/// registered by the owning module. New action types are added by registering a new
/// handler — the scheduler itself never changes.
/// </summary>
public static class TaskActionTypes
{
    /// <summary>Post an in-app/push notification to a user. Handled in Platform. Low risk, runs in-band.</summary>
    public const string NotifyUser = "notify_user";

    /// <summary>Raise a payment <c>Proposal</c> (high risk). Handled in Finance (follow-on). Never moves money in-band.</summary>
    public const string CreatePaymentProposal = "create_payment_proposal";

    /// <summary>Run a domain agent on a schedule. Handled in Agents (follow-on). Mutations flow through the proposal path.</summary>
    public const string RunAgent = "run_agent";
}
