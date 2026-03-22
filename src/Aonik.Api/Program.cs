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

// Add CORS – origins come from configuration so each environment can specify its own list
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allCorsOrigins = corsOrigins
    .Concat(new[] { "http://localhost:5173", "http://localhost:5174" }) // always allow local dev
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Distinct()
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AonikCors", policy =>
    {
        policy.WithOrigins(allCorsOrigins)
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
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "dev")
{
    app.UseAonikSwagger(builder.Configuration);
}

// Map default Aspire endpoints (health, metrics)
app.MapDefaultEndpoints();

// Use HTTPS redirection
app.UseHttpsRedirection();

var logAuthHeaderPresence = builder.Configuration.GetValue<bool>("Auth:Diagnostics:LogHeaderPresence");
if (logAuthHeaderPresence)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isInterestingPath = path.StartsWithSegments("/bootstrap")
            || path.StartsWithSegments("/identity")
            || path.StartsWithSegments("/host");

        var hasAuthorization = context.Request.Headers.ContainsKey("Authorization");
        var hasXAuthorization = context.Request.Headers.ContainsKey("X-Authorization");
        var hasXForwardedAuthorization = context.Request.Headers.ContainsKey("X-Forwarded-Authorization");
        var hasXOriginalAuthorization = context.Request.Headers.ContainsKey("X-Original-Authorization");

        if (isInterestingPath || hasAuthorization || hasXAuthorization || hasXForwardedAuthorization || hasXOriginalAuthorization)
        {
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Aonik.AuthHeaderDiagnostics");

            logger.LogInformation(
                "Request {Method} {Path} header presence: Authorization={HasAuthorization}, X-Authorization={HasXAuthorization}, X-Forwarded-Authorization={HasXForwardedAuthorization}, X-Original-Authorization={HasXOriginalAuthorization}, OriginPresent={HasOrigin}",
                context.Request.Method,
                path,
                hasAuthorization,
                hasXAuthorization,
                hasXForwardedAuthorization,
                hasXOriginalAuthorization,
                context.Request.Headers.ContainsKey("Origin"));
        }

        await next();
    });
}

// Custom CORS middleware — handles both preflight (OPTIONS) and actual requests.
// The built-in UseCors("AonikCors") middleware relies on endpoint metadata that
// FastEndpoints registers too late, so it never adds CORS headers.  This
// middleware replaces it entirely: preflight gets a 204 short-circuit, and actual
// requests get the required Access-Control-Allow-* response headers via
// OnStarting so they are present regardless of downstream behaviour.
var corsOriginsSet = new HashSet<string>(allCorsOrigins, StringComparer.OrdinalIgnoreCase);
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.FirstOrDefault();
    if (!string.IsNullOrEmpty(origin) && corsOriginsSet.Contains(origin))
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            // Preflight — respond immediately without hitting routing/FastEndpoints
            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers",
                "Authorization, Content-Type, X-Tenant-Id, Accept, X-Requested-With");
            context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            context.Response.Headers.Append("Access-Control-Max-Age", "86400");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return; // short-circuit — do NOT call next()
        }

        // Actual request — attach CORS headers to the response just before it is sent.
        // Using OnStarting ensures the headers are present even if an exception filter
        // or other middleware replaces the response.
        context.Response.OnStarting(() =>
        {
            var resp = context.Response;
            if (!resp.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                resp.Headers.Append("Access-Control-Allow-Origin", origin);
                resp.Headers.Append("Access-Control-Allow-Credentials", "true");
                resp.Headers.Append("Vary", "Origin");
            }
            return Task.CompletedTask;
        });
    }

    await next();
});

// Routing + CORS middleware. The custom middleware above handles actual CORS header
// writing via OnStarting; UseCors is still required so ASP.NET Core's
// EndpointMiddleware does not throw when it finds RequireCors metadata on endpoints.
// The custom middleware's ContainsKey guard prevents duplicate headers.
app.UseRouting();
app.UseCors("AonikCors");

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

// 5. FastEndpoints (with global CORS policy applied to all endpoints)
app.UseFastEndpoints(c =>
{
    c.Endpoints.Configurator = ep => ep.Options(b => b.RequireCors("AonikCors"));
});

// 6. AG-UI streaming endpoint (minimal API, separate from FastEndpoints)
app.MapAguiStreaming("/ai/agui")
    .RequireAuthorization("AdminUserPolicy")
    .RequireCors("AonikCors");

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
