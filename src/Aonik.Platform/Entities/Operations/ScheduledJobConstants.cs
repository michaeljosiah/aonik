namespace Aonik.Platform.Entities.Operations;

public static class ScheduledJobGroups
{
    public const string ScheduledJobs = "ScheduledJobs";
}

public static class ScheduledJobStates
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Disabled = "Disabled";
    public const string Missing = "Missing";
    public const string Error = "Error";
    public const string Complete = "Complete";
    public const string Blocked = "Blocked";
}

public static class ScheduledJobCommandTypes
{
    public const string Trigger = "Trigger";
    public const string Pause = "Pause";
    public const string Resume = "Resume";
}

public static class ScheduledJobCommandStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public static class ScheduledJobOutcomeStates
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public static class ScheduledJobRunOutcomes
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Vetoed = "Vetoed";
}

public static class ScheduledJobTriggeredBy
{
    public const string Schedule = "Schedule";
    public const string AdminTrigger = "AdminTrigger";
}
