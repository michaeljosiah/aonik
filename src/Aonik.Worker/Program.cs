using Aonik.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
