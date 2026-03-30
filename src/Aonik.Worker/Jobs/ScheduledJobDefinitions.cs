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
            new ScheduledJobDefinition<BehaviouralInsightJob>(
                BehaviouralInsightJob.Key,
                new TriggerKey("BehaviouralInsightJob-trigger", ScheduledJobGroups.ScheduledJobs),
                "Behavioural Insight",
                "Pre-computes behavioural spending insights for personal finance users.",
                options.BehaviouralInsight.CronExpression,
                options.BehaviouralInsight.Enabled),
        ];
    }
}
