using Aonik.Infrastructure;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Register infrastructure services (database, background jobs, etc.)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Register domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);

// Register the Quartz hosted service
builder.Services.AddHostedService<QuartzHostedService>();

var host = builder.Build();
host.Run();
