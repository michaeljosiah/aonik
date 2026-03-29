using Aonik.Application;
using Aonik.Infrastructure;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Services.Seeding;
using Aonik.Platform.Persistence;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Agents;
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

// Register domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

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
        var dbContextTypes = GetRegisteredDbContextTypes(scope.ServiceProvider);

        foreach (var dbContextType in dbContextTypes)
        {
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);
            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (!pendingMigrations.Any())
            {
                logger.LogInformation("No pending migrations for {DbContext}.", dbContextType.Name);
                continue;
            }

            // If InitialCreate is pending but the schema already exists (created by
            // auto-migrate or a prior deployment), mark it as applied without running
            // its DDL. This avoids "object already exists" errors on consolidated
            // initial migrations.
            await SkipAlreadyAppliedInitialMigrations(dbContext, pendingMigrations, logger);

            pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (!pendingMigrations.Any())
            {
                logger.LogInformation("No remaining pending migrations for {DbContext}.", dbContextType.Name);
                continue;
            }

            logger.LogInformation(
                "Running {Count} pending migration(s) for {DbContext}...",
                pendingMigrations.Count,
                dbContextType.Name);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations for {DbContext} completed successfully.", dbContextType.Name);
        }
    }

    if (runSeed)
    {
        logger.LogInformation("Running seed routines...");

        var platformDbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var identityLogger = loggerFactory.CreateLogger<IdentitySeedService>();
        var identitySeed = new IdentitySeedService(platformDbContext, identityLogger);
        await identitySeed.SeedAsync();

        var catalogLogger = loggerFactory.CreateLogger<CatalogSeedService>();
        var catalogSeed = new CatalogSeedService(platformDbContext, catalogLogger);
        await catalogSeed.SeedAsync();

        var settingsLogger = loggerFactory.CreateLogger<SettingsSeedService>();
        var settingsSeed = new SettingsSeedService(platformDbContext, settingsLogger);
        await settingsSeed.SeedAsync();
    }

    logger.LogInformation("Migrator completed successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Migrator failed.");
    Environment.ExitCode = 1;
}

static List<Type> GetRegisteredDbContextTypes(IServiceProvider serviceProvider)
{
    // Canonical EF migration stream lives in AonikDbContext.
    // Module-scoped DbContexts share this physical database but do not maintain
    // independent migration histories.
    _ = serviceProvider;
    return new List<Type>
    {
        typeof(AonikDbContext)
    };
}

static async Task SkipAlreadyAppliedInitialMigrations(
    DbContext dbContext,
    List<string> pendingMigrations,
    ILogger logger)
{
    // Only act when InitialCreate is pending.
    var initialCreate = pendingMigrations.FirstOrDefault(m => m.EndsWith("_InitialCreate"));
    if (initialCreate is null)
        return;

    // Check whether the schema already exists by probing for a representative table.
    // Use the connection string directly to avoid interfering with EF's internal connection state.
    var connectionString = dbContext.Database.GetConnectionString();
    await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var checkCmd = connection.CreateCommand();
    checkCmd.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'Tenants'";
    var result = await checkCmd.ExecuteScalarAsync();
    var schemaExists = Convert.ToInt32(result) > 0;

    if (!schemaExists)
        return;

    logger.LogWarning(
        "Schema already exists but migration '{Migration}' is pending. " +
        "Recording it as applied without running DDL.",
        initialCreate);

    // Insert the migration into __EFMigrationsHistory so EF considers it applied.
    var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0";
    await using var insertCmd = connection.CreateCommand();
    insertCmd.CommandText =
        $"INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('{initialCreate}', '{productVersion}')";
    await insertCmd.ExecuteNonQueryAsync();

    logger.LogInformation("Recorded '{Migration}' as applied.", initialCreate);
}
