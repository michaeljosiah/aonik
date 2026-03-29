using Aonik.Infrastructure;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Agents;
using Aonik.Worker;

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

// Register the Quartz hosted service
builder.Services.AddHostedService<QuartzHostedService>();
builder.Services.AddHostedService<FinancialConnectionRecurringSyncWorker>();

// User Brief: stale session detection and conversation summary generation
builder.Services.AddHostedService<StaleSessionDetectorWorker>();

// Behavioural insight pre-computation (runs every 6 hours)
builder.Services.AddHostedService<BehaviouralInsightWorker>();

var host = builder.Build();
host.Run();
