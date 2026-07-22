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
    public DocumentIngestionBackfillJobOptions DocumentIngestionBackfill { get; set; } = new();
    public WorkItemDispatchJobOptions WorkItemDispatch { get; set; } = new();
    public InventoryReservationSweepJobOptions InventoryReservationSweep { get; set; } = new();
    public LowStockScanJobOptions LowStockScan { get; set; } = new();
    public BoxCartAbandonSweepJobOptions BoxCartAbandonSweep { get; set; } = new();
}

public sealed class InventoryReservationSweepJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 5 minutes — frequent enough to
    /// free stock from abandoned checkouts soon after the 30-minute reservation TTL lapses (Spec 042 §10).
    /// </summary>
    public string CronExpression { get; set; } = "0 0/5 * * * ?";
}

public sealed class LowStockScanJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 15 minutes — a reorder decision
    /// is a same-day signal, not a realtime one; the scan is idempotent so cadence only affects how
    /// soon a breach surfaces (Spec 052 §9).
    /// </summary>
    public string CronExpression { get; set; } = "0 0/15 * * * ?";
}

public sealed class WorkItemDispatchJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 60 seconds — the
    /// minute-level granularity the task scheduler targets (Spec 034).
    /// </summary>
    public string CronExpression { get; set; } = "0 * * * * ?";

    /// <summary>Max due work items claimed per sweep.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>How long a claimed work item is hidden from other workers (lease window, seconds).</summary>
    public int LeaseSeconds { get; set; } = 300;

    /// <summary>Attempts per occurrence before it is failed (one-off) or skipped (recurring).</summary>
    public int MaxAttempts { get; set; } = 5;
}

public sealed class DocumentIngestionBackfillJobOptions
{
    /// <summary>
    /// Disabled by default (Spec 035 Phase 4, optional). An operator enables this to drain
    /// documents that never completed ingestion, then disables it again — the job is self-limiting.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: every 10 minutes (only fires when enabled).
    /// </summary>
    public string CronExpression { get; set; } = "0 0/10 * * * ?";

    public int BatchSize { get; set; } = 200;
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

public sealed class BoxCartAbandonSweepJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Quartz cron expression (6-field with seconds). Default: daily at 03:10 — abandonment is a
    /// days-scale window (Spec 068 A6, Commerce.Carts.AbandonAfterDays, default 14), so a daily
    /// pass is ample; the transition is idempotent.
    /// </summary>
    public string CronExpression { get; set; } = "0 10 3 * * ?";
}
