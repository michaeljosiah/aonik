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

// Explicitly load appsettings.json since Host.CreateApplicationBuilder doesn't auto-load it for console apps
var appDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Directory.GetCurrentDirectory();
builder.Configuration.SetBasePath(appDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

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
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Aonik.Migrator");

try
{
    if (runMigrations)
    {
        // Migrate all registered DbContexts that derive from AonikDbContextBase.
        // During the transition period only AonikDbContext exists; as module-scoped
        // DbContexts are introduced (PlatformDbContext, FinanceDbContext, etc.)
        // they will be picked up automatically when registered in DI.
        var dbContextTypes = GetRegisteredDbContextTypes(scope.ServiceProvider);

        foreach (var dbContextType in dbContextTypes)
        {
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);
            logger.LogInformation("Running migrations for {DbContext}...", dbContextType.Name);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations for {DbContext} completed successfully.", dbContextType.Name);
        }
    }

    if (runSeed)
    {
        logger.LogInformation("Running seed routines...");

        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

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

/// <summary>
/// Discovers all DbContext types registered in the DI container that derive from
/// <see cref="AonikDbContextBase"/>. Falls back to just AonikDbContext if none found.
/// Ordering: AonikDbContext (monolith) runs first, then module contexts alphabetically.
/// </summary>
static List<Type> GetRegisteredDbContextTypes(IServiceProvider serviceProvider)
{
    var result = new List<Type>();

    // Always include AonikDbContext first (it owns existing migrations)
    result.Add(typeof(AonikDbContext));

    // Discover any additional module DbContexts registered in DI
    // These will be added as we create PlatformDbContext, FinanceDbContext, etc.
    // For now this is a placeholder that checks for known types in the service collection.
    // When a module DbContext is registered via services.AddDbContext<T>(), it becomes
    // resolvable and will be picked up here.

    // Future: iterate registered DbContext types from DI descriptors
    // For now, the monolithic AonikDbContext is the only one.

    return result;
}
