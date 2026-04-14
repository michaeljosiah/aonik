namespace Aonik.Worker.Jobs;

/// <summary>
/// Configuration for all Quartz-scheduled background jobs.
/// Bound to the "Quartz:ScheduledJobs" configuration section.
/// </summary>
public sealed class ScheduledJobOptions
{
    public FinancialConnectionSyncJobOptions FinancialConnectionSync { get; set; } = new();
    public StaleSessionDetectorJobOptions StaleSessionDetector { get; set; } = new();
    public CustomerInsightSnapshotJobOptions CustomerInsightSnapshot { get; set; } = new();
    public CustomerInsightAiSummaryJobOptions CustomerInsightAiSummary { get; set; } = new();
    public AiCostGuardJobOptions AiCostGuard { get; set; } = new();
}

public sealed class AiCostGuardJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 15 minutes.
    /// </summary>
    public string CronExpression { get; set; } = "0 0/15 * * * ?";

    /// <summary>
    /// Time range to evaluate against — uses the same vocabulary as the
    /// observability AI tab (e.g. "1h", "6h", "24h").
    /// </summary>
    public string TimeRange { get; set; } = "1h";

    /// <summary>
    /// Estimated USD cost over <see cref="TimeRange"/> that should trip the
    /// cost guard. The threshold is intentionally low — the runaway-spend
    /// incident burned £10 in well under an hour.
    /// </summary>
    public double ThresholdUsd { get; set; } = 5.0;
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

    /// <summary>
    /// Agent names whose conversations should be summarised.
    /// Empty list = no conversations are summarised.
    /// </summary>
    public List<string> AgentNames { get; set; } = [];
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

public sealed class CustomerInsightAiSummaryJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 30 minutes.
    /// </summary>
    public string CronExpression { get; set; } = "0 0/30 * * * ?";

    public int BatchSize { get; set; } = 50;

    public int SnapshotWarningThresholdSeconds { get; set; } = 20;

    public int SnapshotTimeoutSeconds { get; set; } = 90;
}
