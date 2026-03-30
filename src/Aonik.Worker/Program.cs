using Aonik.Infrastructure;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Agents;
using Aonik.Worker.Jobs;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Register infrastructure services (database, background jobs, etc.)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Register domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

// Bind scheduled job options
builder.Services.Configure<ScheduledJobOptions>(
    builder.Configuration.GetSection("Quartz:ScheduledJobs"));

// Read job options for Quartz configuration
var jobOptions = builder.Configuration
    .GetSection("Quartz:ScheduledJobs")
    .Get<ScheduledJobOptions>() ?? new ScheduledJobOptions();

// Register job listener and startup registrar
builder.Services.AddSingleton<ScheduledJobListener>();
builder.Services.AddHostedService<ScheduledJobRegistrar>();

// Configure Quartz scheduler with cron-scheduled jobs
builder.Services.AddQuartz(q =>
{
    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 3);

    // Add listener that updates Job entity after each execution
    q.AddJobListener<ScheduledJobListener>();

    if (jobOptions.FinancialConnectionSync.Enabled)
    {
        var jobKey = new JobKey("FinancialConnectionRecurringSyncJob", "ScheduledJobs");
        q.AddJob<FinancialConnectionRecurringSyncJob>(opts => opts
            .WithIdentity(jobKey)
            .WithDescription("Synchronises linked financial account transactions for connections due for recurring sync."));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("FinancialConnectionSync-trigger", "ScheduledJobs")
            .WithCronSchedule(jobOptions.FinancialConnectionSync.CronExpression));
    }

    if (jobOptions.StaleSessionDetector.Enabled)
    {
        var jobKey = new JobKey("StaleSessionDetectorJob", "ScheduledJobs");
        q.AddJob<StaleSessionDetectorJob>(opts => opts
            .WithIdentity(jobKey)
            .WithDescription("Detects stale chat sessions and generates conversation summaries."));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("StaleSessionDetector-trigger", "ScheduledJobs")
            .WithCronSchedule(jobOptions.StaleSessionDetector.CronExpression));
    }

    if (jobOptions.BehaviouralInsight.Enabled)
    {
        var jobKey = new JobKey("BehaviouralInsightJob", "ScheduledJobs");
        q.AddJob<BehaviouralInsightJob>(opts => opts
            .WithIdentity(jobKey)
            .WithDescription("Pre-computes behavioural spending insights for personal finance users."));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("BehaviouralInsight-trigger", "ScheduledJobs")
            .WithCronSchedule(jobOptions.BehaviouralInsight.CronExpression));
    }
});

// Quartz hosted service manages scheduler lifecycle (start, graceful shutdown)
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var host = builder.Build();
host.Run();
