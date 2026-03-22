using Aonik.Api.Configuration;
using Aonik.Api.Middleware;
using Aonik.Application;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure;
using FastEndpoints;
using System.Data.Common;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Services.Seeding;
using Aonik.Platform.Persistence;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Agents;
using Aonik.Agents.Endpoints;


var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Add domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add AONIK Authentication & Authorization
builder.Services.AddAonikAuthenticationAndAuthorization(builder.Configuration);

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Use string enums in JSON
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add Swagger with OAuth2 authentication
builder.Services.AddAonikSwagger(builder.Configuration);

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    LogResolvedDatabaseConnection(startupScope.ServiceProvider, startupLogger);
}

// Auto-migrate and seed database in Development or when enabled via config
var autoMigrateEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:AutoMigrate");
var seedDataEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:SeedData");

if (autoMigrateEnabled || seedDataEnabled)
{
    using var scope = app.Services.CreateScope();
    var platformDbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (autoMigrateEnabled)
        {
            startupLogger.LogInformation("Running database migrations...");

            var dbContextTypes = GetRegisteredDbContextTypes(scope.ServiceProvider);
            foreach (var dbContextType in dbContextTypes)
            {
                var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

                if (!pendingMigrations.Any())
                {
                    startupLogger.LogInformation("No pending migrations for {DbContext}.", dbContextType.Name);
                    continue;
                }

                startupLogger.LogInformation(
                    "Applying {Count} pending migration(s) for {DbContext}...",
                    pendingMigrations.Count(),
                    dbContextType.Name);
                await dbContext.Database.MigrateAsync();
                startupLogger.LogInformation("Migrations for {DbContext} completed successfully.", dbContextType.Name);
            }

            startupLogger.LogInformation("Database migrations completed successfully.");
        }

        if (seedDataEnabled)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentitySeedService>>();
            var seedService = new IdentitySeedService(platformDbContext, logger);
            await seedService.SeedAsync();

            var catalogLogger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogSeedService>>();
            var catalogSeedService = new CatalogSeedService(platformDbContext, catalogLogger);
            await catalogSeedService.SeedAsync();

            var settingsLogger = scope.ServiceProvider.GetRequiredService<ILogger<SettingsSeedService>>();
            var settingsSeedService = new SettingsSeedService(platformDbContext, settingsLogger);
            await settingsSeedService.SeedAsync();

            startupLogger.LogInformation("Database seed routines completed successfully.");
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Skipping database initialization due to connectivity issues.");
    }
}



// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseAonikSwagger(builder.Configuration);
}

// Map default Aspire endpoints (health, metrics)
app.MapDefaultEndpoints();

// Use HTTPS redirection
app.UseHttpsRedirection();

// Use CORS for development
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}

// Serve static files for local blob storage (profile photos, etc.)
// Only use local file storage in Development; deployed environments should use Azure Blob Storage
var blobStorageProvider = builder.Configuration["BlobStorage:Provider"];
if (app.Environment.IsDevelopment() && string.Equals(blobStorageProvider, "Local", StringComparison.OrdinalIgnoreCase))
{
    var localBasePath = builder.Configuration["BlobStorage:LocalBasePath"] ?? "App_Data";
    var profilePhotosPath = builder.Configuration["BlobStorage:ProfilePhotos:Path"] ?? "profiles";
    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), localBasePath, profilePhotosPath);
    
    // Create directory if it doesn't exist
    Directory.CreateDirectory(physicalPath);
    
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(physicalPath),
        RequestPath = "/storage/profiles",
        OnPrepareResponse = ctx =>
        {
            // Cache images for 1 hour
            ctx.Context.Response.Headers.CacheControl = "public, max-age=3600";
        }
    });

    // Serve attachment files (transaction receipts, etc.)
    var attachmentsPath = builder.Configuration["BlobStorage:Attachments:Path"] ?? "attachments";
    var attachmentsPhysicalPath = Path.Combine(Directory.GetCurrentDirectory(), localBasePath, attachmentsPath);
    Directory.CreateDirectory(attachmentsPhysicalPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(attachmentsPhysicalPath),
        RequestPath = "/storage/attachments",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=3600";
        }
    });
}

// CRITICAL: Middleware order matters!
// 1. Authentication (validates JWT, runs OnTokenValidated)
app.UseAuthentication();

// 2. Tenant context resolution
app.UseTenantContext();

// 3. Authorization (checks policies/permissions)
app.UseAuthorization();

// 4. Tenant validation (validates tenant status only)
app.UseTenantValidation();

// 5. FastEndpoints
app.UseFastEndpoints();

// 6. AG-UI streaming endpoint (minimal API, separate from FastEndpoints)
app.MapAguiStreaming("/ai/agui")
    .RequireAuthorization("AdminUserPolicy");

app.Run();

static void LogResolvedDatabaseConnection(IServiceProvider serviceProvider, ILogger logger)
{
    var dbContext = serviceProvider.GetService<AonikDbContext>();
    if (dbContext is null)
    {
        logger.LogWarning("AonikDbContext is not registered; skipping database connection diagnostics.");
        return;
    }

    if (!dbContext.Database.IsRelational())
    {
        logger.LogInformation(
            "Resolved Aonik database provider: {ProviderName} (non-relational)",
            dbContext.Database.ProviderName ?? "unknown");
        return;
    }

    var connectionString = dbContext.Database.GetConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        logger.LogWarning("No database connection string resolved for AonikDbContext.");
        return;
    }

    var (server, database, authentication) = ParseConnectionInfo(connectionString);
    logger.LogInformation(
        "Resolved Aonik SQL connection: server={Server}; database={Database}; auth={Authentication}",
        server,
        database,
        authentication);
}

static (string Server, string Database, string Authentication) ParseConnectionInfo(string connectionString)
{
    var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

    var server = GetConnectionValue(builder, "Data Source", "Server", "Address", "Addr", "Network Address")
        ?? "(unknown)";
    var database = GetConnectionValue(builder, "Initial Catalog", "Database")
        ?? "(unknown)";

    var integratedSecurityValue = GetConnectionValue(builder, "Integrated Security", "Trusted_Connection");
    var isIntegratedSecurity = IsTrue(integratedSecurityValue)
        || string.Equals(integratedSecurityValue, "SSPI", StringComparison.OrdinalIgnoreCase);

    var authentication = isIntegratedSecurity
        ? "IntegratedSecurity"
        : "SqlAuth";

    return (server, database, authentication);
}

static string? GetConnectionValue(DbConnectionStringBuilder builder, params string[] keys)
{
    foreach (var key in keys)
    {
        if (builder.TryGetValue(key, out var value) && value is not null)
        {
            return Convert.ToString(value);
        }
    }

    return null;
}

static bool IsTrue(string? value)
{
    return value is not null
           && (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase));
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

// Make the Program class accessible for testing
public partial class Program { }
