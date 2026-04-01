namespace Aonik.Worker.Jobs;

/// <summary>
/// Configuration for all Quartz-scheduled background jobs.
/// Bound to the "Quartz:ScheduledJobs" configuration section.
/// </summary>
public sealed class ScheduledJobOptions
{
    public FinancialConnectionSyncJobOptions FinancialConnectionSync { get; set; } = new();
    public StaleSessionDetectorJobOptions StaleSessionDetector { get; set; } = new();
    public BehaviouralInsightJobOptions BehaviouralInsight { get; set; } = new();
    public CustomerInsightSnapshotJobOptions CustomerInsightSnapshot { get; set; } = new();
}

public sealed class FinancialConnectionSyncJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 60 seconds.
    /// </summary>
    public string CronExpression { get; set; } = "0 * * * * ?";

    public int BatchSize { get; set; } = 25;
}

public sealed class StaleSessionDetectorJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 5 minutes.
    /// </summary>
    public string CronExpression { get; set; } = "0 0/5 * * * ?";

    public int BatchSize { get; set; } = 10;
}

public sealed class BehaviouralInsightJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 6 hours.
    /// </summary>
    public string CronExpression { get; set; } = "0 0 0/6 * * ?";

    public int MaxUsers { get; set; } = 100;
}

public sealed class CustomerInsightSnapshotJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 15 minutes.
    /// </summary>
    public string CronExpression { get; set; } = "0 0/15 * * * ?";

    public int BatchSize { get; set; } = 50;

    public int UserWarningThresholdSeconds { get; set; } = 10;

    public int UserTimeoutSeconds { get; set; } = 60;
}
