using Aonik.Application;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

using var host = builder.Build();

var runMigrations = !args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase);
var runSeed = !args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);

var configMigrate = builder.Configuration.GetValue<bool?>("Migrator:RunMigrations");
if (configMigrate.HasValue)
{
    runMigrations = configMigrate.Value;
}

var configSeed = builder.Configuration.GetValue<bool?>("Migrator:SeedData");
if (configSeed.HasValue)
{
    runSeed = configSeed.Value;
}

if (!runMigrations && !runSeed)
{
    Console.WriteLine("No actions selected. Use --migrate-only or --seed-only, or configure Migrator:RunMigrations/SeedData.");
    return;
}

using var scope = host.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Aonik.Migrator");

try
{
    if (runMigrations)
    {
        logger.LogInformation("Running database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations completed successfully.");
    }

    if (runSeed)
    {
        logger.LogInformation("Running seed routines...");

        var identityLogger = loggerFactory.CreateLogger<IdentitySeedService>();
        var identitySeed = new IdentitySeedService((IAonikDbContext)dbContext, identityLogger);
        await identitySeed.SeedAsync();

        var catalogLogger = loggerFactory.CreateLogger<CatalogSeedService>();
        var catalogSeed = new CatalogSeedService((IAonikDbContext)dbContext, catalogLogger);
        await catalogSeed.SeedAsync();
    }

    logger.LogInformation("Migrator completed successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Migrator failed.");
    Environment.ExitCode = 1;
}
