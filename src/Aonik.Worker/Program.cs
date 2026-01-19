using Aonik.Infrastructure;
using Aonik.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Register infrastructure services (database, background jobs, etc.)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Register the Quartz hosted service
builder.Services.AddHostedService<QuartzHostedService>();

var host = builder.Build();
host.Run();
