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
            new ScheduledJobDefinition<SubscriptionRenewalJob>(
                SubscriptionRenewalJob.Key,
                new TriggerKey("SubscriptionRenewalJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Subscription Renewal",
                "Bills subscriptions whose period is due and closes those cancelled at the boundary (Spec 087).",
                options.SubscriptionRenewal.CronExpression,
                options.SubscriptionRenewal.Enabled),
            new ScheduledJobDefinition<SubscriptionDunningJob>(
                SubscriptionDunningJob.Key,
                new TriggerKey("SubscriptionDunningJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Subscription Dunning",
                "Retries subscriptions whose payment failed, and expires those that exhaust their attempts (Spec 087).",
                options.SubscriptionDunning.CronExpression,
                options.SubscriptionDunning.Enabled),
            new ScheduledJobDefinition<UsageReservationSweepJob>(
                UsageReservationSweepJob.Key,
                new TriggerKey("UsageReservationSweepJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Usage Reservation Sweep",
                "Returns entitlement holds left behind by dispatches that never finished (Spec 087).",
                options.UsageReservationSweep.CronExpression,
                options.UsageReservationSweep.Enabled),
            new ScheduledJobDefinition<GrantExpirySweepJob>(
                GrantExpirySweepJob.Key,
                new TriggerKey("GrantExpirySweepJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Grant Expiry Sweep",
                "Closes lapsed entitlement grants so breakage is recorded rather than inferred (Spec 087).",
                options.GrantExpirySweep.CronExpression,
                options.GrantExpirySweep.Enabled),
            new ScheduledJobDefinition<SafetyRetentionJob>(
                SafetyRetentionJob.Key,
                new TriggerKey("SafetyRetentionJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Safety Retention Sweep",
                "Deletes expired blocked content and anonymises expired safety decisions, skipping legal holds (Spec 096 §13).",
                options.SafetyRetention.CronExpression,
                options.SafetyRetention.Enabled),
            new ScheduledJobDefinition<AgeTransitionJob>(
                AgeTransitionJob.Key,
                new TriggerKey("AgeTransitionJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Age Transitions",
                "Lapses guardian consent at the consent age and ends guardianship at majority, and moves safety bands (Spec 095 §11).",
                options.AgeTransition.CronExpression,
                options.AgeTransition.Enabled),
            new ScheduledJobDefinition<GroupPartyBackfillJob>(
                GroupPartyBackfillJob.Key,
                new TriggerKey("GroupPartyBackfillJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Group Party Backfill",
                "Populates group party ids, kind, resource kind and terms ahead of the Spec 086 reader cutover (one-off; disabled by default).",
                options.GroupPartyBackfill.CronExpression,
                options.GroupPartyBackfill.Enabled),
            new ScheduledJobDefinition<CanonicalLedgerBackfillJob>(
                CanonicalLedgerBackfillJob.Key,
                new TriggerKey("CanonicalLedgerBackfillJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Canonical Ledger Backfill",
                "Marks each tenant's canonical ledger so ILedgerResolver can answer (Spec 088; one-off, disabled by default).",
                options.CanonicalLedgerBackfill.CronExpression,
                options.CanonicalLedgerBackfill.Enabled),
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
