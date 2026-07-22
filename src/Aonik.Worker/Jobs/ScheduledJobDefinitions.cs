using Aonik.Platform.Entities.Operations;
using Quartz;

namespace Aonik.Worker.Jobs;

internal interface IScheduledJobDefinition
{
    JobKey JobKey { get; }
    TriggerKey TriggerKey { get; }
    string DisplayName { get; }
    string Description { get; }
    string CronExpression { get; }
    string TimeZoneId { get; }
    bool Enabled { get; }

    void Configure(IServiceCollectionQuartzConfigurator quartz);
}

internal sealed class ScheduledJobDefinition<TJob> : IScheduledJobDefinition where TJob : IJob
{
    private readonly TimeZoneInfo _timeZone;

    public ScheduledJobDefinition(
        JobKey jobKey,
        TriggerKey triggerKey,
        string displayName,
        string description,
        string cronExpression,
        bool enabled,
        TimeZoneInfo? timeZone = null)
    {
        JobKey = jobKey;
        TriggerKey = triggerKey;
        DisplayName = displayName;
        Description = description;
        CronExpression = cronExpression;
        Enabled = enabled;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    public JobKey JobKey { get; }

    public TriggerKey TriggerKey { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string CronExpression { get; }

    public string TimeZoneId => _timeZone.Id;

    public bool Enabled { get; }

    public void Configure(IServiceCollectionQuartzConfigurator quartz)
    {
        if (!Enabled)
        {
            return;
        }

        quartz.AddJob<TJob>(opts => opts
            .WithIdentity(JobKey)
            .WithDescription(Description)
            .StoreDurably());

        quartz.AddTrigger(opts => opts
            .ForJob(JobKey)
            .WithIdentity(TriggerKey)
            .WithDescription(DisplayName)
            .WithCronSchedule(CronExpression, cron => cron
                .InTimeZone(_timeZone)
                .WithMisfireHandlingInstructionDoNothing()));
    }
}

internal static class ScheduledJobDefinitions
{
    public static IReadOnlyList<IScheduledJobDefinition> Create(ScheduledJobOptions options)
    {
        return
        [
            new ScheduledJobDefinition<FinancialConnectionRecurringSyncJob>(
                FinancialConnectionRecurringSyncJob.Key,
                new TriggerKey("FinancialConnectionRecurringSyncJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Financial Connection Recurring Sync",
                "Synchronises linked financial account transactions for connections due for recurring sync.",
                options.FinancialConnectionSync.CronExpression,
                options.FinancialConnectionSync.Enabled),
            new ScheduledJobDefinition<StaleSessionDetectorJob>(
                StaleSessionDetectorJob.Key,
                new TriggerKey("StaleSessionDetectorJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Stale Session Detector",
                "Detects stale chat sessions and generates conversation summaries.",
                options.StaleSessionDetector.CronExpression,
                options.StaleSessionDetector.Enabled),
            new ScheduledJobDefinition<CustomerInsightSnapshotJob>(
                CustomerInsightSnapshotJob.Key,
                new TriggerKey("CustomerInsightSnapshotJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Customer Insight Snapshot",
                "Generates deterministic customer insight snapshots for eligible personal finance users.",
                options.CustomerInsightSnapshot.CronExpression,
                options.CustomerInsightSnapshot.Enabled),
            new ScheduledJobDefinition<CustomerInsightAiSummaryJob>(
                CustomerInsightAiSummaryJob.Key,
                new TriggerKey("CustomerInsightAiSummaryJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Customer Insight AI Summary",
                "Generates AI interpretations from deterministic customer insight snapshots.",
                options.CustomerInsightAiSummary.CronExpression,
                options.CustomerInsightAiSummary.Enabled),
            new ScheduledJobDefinition<AiCostGuardJob>(
                AiCostGuardJob.Key,
                new TriggerKey("AiCostGuardJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "AI Cost Guard",
                "Polls AiCallCompleted spend and emits a high-priority alert when the configured threshold is exceeded.",
                options.AiCostGuard.CronExpression,
                options.AiCostGuard.Enabled),
            new ScheduledJobDefinition<DocumentIngestionBackfillJob>(
                DocumentIngestionBackfillJob.Key,
                new TriggerKey("DocumentIngestionBackfillJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Document Ingestion Backfill",
                "Re-publishes DocumentUploadedEvent for indexable documents that never completed ingestion (opt-in catch-up; disabled by default).",
                options.DocumentIngestionBackfill.CronExpression,
                options.DocumentIngestionBackfill.Enabled),
            new ScheduledJobDefinition<WorkItemDispatchJob>(
                WorkItemDispatchJob.Key,
                new TriggerKey("WorkItemDispatchJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Work Item Dispatch",
                "Fires due Spec 034 tasks (reminders, scheduled actions, agent jobs) across all tenants every minute.",
                options.WorkItemDispatch.CronExpression,
                options.WorkItemDispatch.Enabled),
            new ScheduledJobDefinition<InventoryReservationSweepJob>(
                InventoryReservationSweepJob.Key,
                new TriggerKey("InventoryReservationSweepJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Inventory Reservation Sweep",
                "Releases expired held inventory reservations so abandoned checkouts free stock (Spec 042).",
                options.InventoryReservationSweep.CronExpression,
                options.InventoryReservationSweep.Enabled),
            new ScheduledJobDefinition<LowStockScanJob>(
                LowStockScanJob.Key,
                new TriggerKey("LowStockScanJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Low Stock Scan",
                "Raises or refreshes low-stock alerts for ingredient levels at or below their reorder point (Spec 052).",
                options.LowStockScan.CronExpression,
                options.LowStockScan.Enabled),
            new ScheduledJobDefinition<BoxCartAbandonSweepJob>(
                BoxCartAbandonSweepJob.Key,
                new TriggerKey("BoxCartAbandonSweepJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Box Cart Abandon Sweep",
                "Transitions box sessions idle beyond the configured window to Abandoned (Spec 068 A6).",
                options.BoxCartAbandonSweep.CronExpression,
                options.BoxCartAbandonSweep.Enabled),
        ];
    }
}
