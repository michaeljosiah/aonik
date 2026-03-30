using Aonik.Infrastructure;
using Aonik.Infrastructure.BackgroundJobs;
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

// Read Quartz persistence options
var quartzPersistenceEnabled = builder.Configuration.GetValue<bool>("Quartz:Persistence:Enabled");
var quartzTablePrefix = builder.Configuration.GetValue<string>("Quartz:Persistence:TablePrefix") ?? "QRTZ_";
var quartzSchedulerName = builder.Configuration.GetValue<string>("Quartz:Persistence:SchedulerName") ?? "AonikScheduler";
var quartzMisfireThresholdSeconds = builder.Configuration.GetValue<int>("Quartz:Persistence:MisfireThresholdSeconds", 60);

// Read job options for Quartz configuration
var jobOptions = builder.Configuration
    .GetSection("Quartz:ScheduledJobs")
    .Get<ScheduledJobOptions>() ?? new ScheduledJobOptions();

var scheduledJobDefinitions = ScheduledJobDefinitions.Create(jobOptions);

foreach (var definition in scheduledJobDefinitions)
{
    builder.Services.AddSingleton<IScheduledJobDefinition>(definition);
}

// Register the Quartz runtime only in the Worker host.
builder.Services.AddQuartzBackgroundJobRuntime();

// Register job listener, projection sync, and admin command processing.
builder.Services.AddSingleton<ScheduledJobListener>();
builder.Services.AddSingleton<ScheduledJobProjectionSynchronizer>();
builder.Services.AddHostedService<ScheduledJobRegistrar>();
builder.Services.AddHostedService<ScheduledJobCommandProcessor>();
builder.Services.AddHostedService<SchedulerHealthPublisher>();

// Configure Quartz scheduler with cron-scheduled jobs
builder.Services.AddQuartz(q =>
{
    q.SchedulerName = quartzSchedulerName;
    q.SchedulerId = "AUTO";
    q.MisfireThreshold = TimeSpan.FromSeconds(quartzMisfireThresholdSeconds);

    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 3);

    if (quartzPersistenceEnabled)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration.GetConnectionString("AonikDb");

        q.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.UseSqlServer(sql =>
            {
                sql.ConnectionString = connectionString!;
                sql.TablePrefix = quartzTablePrefix;
            });
            store.UseSystemTextJsonSerializer();
            store.PerformSchemaValidation = true;
        });
    }

    // Refresh projection state after each scheduled execution.
    q.AddJobListener<ScheduledJobListener>();

    foreach (var definition in scheduledJobDefinitions)
    {
        definition.Configure(q);
    }
});

// Quartz hosted service manages scheduler lifecycle (start, graceful shutdown)
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var host = builder.Build();
host.Run();
