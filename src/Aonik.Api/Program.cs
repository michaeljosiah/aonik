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
using Aonik.Platform.Endpoints.Admin.Notifications;
using Aonik.Platform.Entities.Identity;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.Ai.Services.Seeding;
using Aonik.Agents;
using Aonik.Agents.Endpoints;
using Microsoft.AspNetCore.HttpOverrides;


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
    .Concat(new[]
    {
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:4201",
        "http://127.0.0.1:4201"
    }) // always allow local dev
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

// Add FastEndpoints. Explicitly enumerate the module assemblies so that
// endpoints AND validators (Validator<TRequest>) defined in each module
// are discovered at startup — relying on AppDomain probing alone is
// fragile because module DLLs are not loaded until their first reference.
builder.Services.AddFastEndpoints(o =>
{
    o.Assemblies =
    [
        typeof(Aonik.Platform.PlatformModule).Assembly,
        typeof(Aonik.Finance.FinanceModule).Assembly,
        typeof(Aonik.Ai.AiModule).Assembly,
        typeof(Aonik.Agents.AgentsModule).Assembly,
    ];
});

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

            // Ensure PlatformAdmin role has role-permission mappings.
            // IdentitySeedService only seeds Permission rows; if the platform was
            // bootstrapped before SeedData was enabled, the PlatformAdmin role
            // exists but has zero RolePermission records, blocking all API calls.
            await EnsurePlatformAdminRolePermissionsAsync(platformDbContext, startupLogger);

            // Top up tenant TenantAdmin roles with permissions added since the
            // tenant was first provisioned. EnsureDefaultRolePermissionsAsync
            // (in TenantProvisioner) only runs at provisioning time, so a
            // tenant created BEFORE a new permission was added (e.g. Catalog.
            // Write) ends up missing it. This pass walks every tenant once
            // per startup and inserts any missing role-permission rows.
            await EnsureTenantRolePermissionsUpToDateAsync(platformDbContext, startupLogger);

            var catalogLogger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogSeedService>>();
            var catalogSeedService = new CatalogSeedService(platformDbContext, catalogLogger);
            await catalogSeedService.SeedAsync();

            var settingsLogger = scope.ServiceProvider.GetRequiredService<ILogger<SettingsSeedService>>();
            var settingsSeedService = new SettingsSeedService(platformDbContext, settingsLogger);
            await settingsSeedService.SeedAsync();

            var notificationTemplateLogger = scope.ServiceProvider.GetRequiredService<ILogger<NotificationTemplateSeedService>>();
            var notificationTemplateSeedService = new NotificationTemplateSeedService(platformDbContext, notificationTemplateLogger);
            await notificationTemplateSeedService.SeedAsync();

            var aiDbContext = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            var fileBasedPromptStore = scope.ServiceProvider.GetRequiredService<FileBasedPromptStore>();
            var promptSeedLogger = scope.ServiceProvider.GetRequiredService<ILogger<PromptSpecSeedService>>();
            var promptSeedService = new PromptSpecSeedService(aiDbContext, fileBasedPromptStore, promptSeedLogger);
            await promptSeedService.SeedAsync();

            var aiTaskSeedLogger = scope.ServiceProvider.GetRequiredService<ILogger<AiTaskSeedService>>();
            var aiTaskSeedService = new AiTaskSeedService(aiDbContext, aiTaskSeedLogger);
            await aiTaskSeedService.SeedAsync();

            startupLogger.LogInformation("Database seed routines completed successfully.");
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Skipping database initialization due to connectivity issues.");
    }
}



// Configure the HTTP request pipeline — Swagger/Scalar setup is deferred
// until after routing so MapScalarApiReference can register its endpoint.

// Map default Aspire endpoints (health, metrics)
app.MapDefaultEndpoints();

// Forward headers so ASP.NET Core recognises the original HTTPS scheme behind ACA's TLS-terminating ingress
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Custom CORS middleware — handles both preflight (OPTIONS) and actual requests.
// Placed before UseHttpsRedirection so OPTIONS preflight is short-circuited before
// any redirect can fire (ACA terminates TLS and forwards plain HTTP on port 8080).
// The built-in UseCors("AonikCors") middleware relies on endpoint metadata that
// FastEndpoints registers too late, so it never adds CORS headers.  This
// middleware replaces it entirely: preflight gets a 204 short-circuit, and actual
// requests get the required Access-Control-Allow-* response headers via
// OnStarting so they are present regardless of downstream behaviour.
var corsOriginsSet = new HashSet<string>(allCorsOrigins, StringComparer.OrdinalIgnoreCase);
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.FirstOrDefault();
    var isAllowedCorsOrigin = !string.IsNullOrEmpty(origin) && corsOriginsSet.Contains(origin);
    if (isAllowedCorsOrigin && origin is not null)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            // Preflight — respond immediately without hitting routing/FastEndpoints
            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers",
                "Authorization, Content-Type, X-Tenant-Id, Accept, X-Requested-With, X-AgUi-Trace-Id");
            context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            context.Response.Headers.Append("Access-Control-Max-Age", "86400");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return; // short-circuit — do NOT call next()
        }

        // Actual request — apply CORS headers immediately so error responses still
        // include them if downstream middleware/handlers fail before the response starts.
        ApplyActualCorsHeaders(context.Response, origin);
    }

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Log the exception FIRST, before any CORS / response branching.
        // The previous shape only logged when CORS conditions matched, so
        // same-origin failures (where the browser doesn't send Origin)
        // silently re-threw without an application-level log entry. That
        // left admins staring at a generic 500 in the network tab with
        // no matching row in the Logs page. The dedicated category name
        // makes it trivially KQL-filterable:
        //   traces | where customDimensions.CategoryName == "Aonik.UnhandledException"
        var logger = context.RequestServices.GetService<ILoggerFactory>()
            ?.CreateLogger("Aonik.UnhandledException");
        // Walk the exception chain so root-cause messages (e.g. SQL
        // constraint violations buried inside DbUpdateException) make
        // it into the structured log without forcing the operator to
        // open the formatted exception block.
        static string FlattenChain(Exception root)
        {
            var sb = new System.Text.StringBuilder();
            for (var current = root; current != null; current = current.InnerException)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(current.GetType().FullName).Append(": ").Append(current.Message);
            }
            return sb.ToString();
        }
        var innermost = ex;
        while (innermost.InnerException is not null) innermost = innermost.InnerException;

        logger?.LogError(
            ex,
            "Unhandled exception on {Method} {Path} (status=500, exceptionType={ExceptionType}, exceptionMessage={ExceptionMessage}, innerType={InnerType}, innerMessage={InnerMessage}, chain={ExceptionChain})",
            context.Request.Method,
            context.Request.Path,
            ex.GetType().FullName,
            ex.Message,
            innermost.GetType().FullName,
            innermost.Message,
            FlattenChain(ex));

        // Stamp the active OTel span with error tags too, so the trace
        // explorer shows the failure inline on the request span — users
        // can click the failing trace and see the exception type without
        // pivoting to the Logs page.
        var activity = System.Diagnostics.Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("error", true);
            activity.SetTag("error.type", ex.GetType().FullName);
            activity.SetTag("error.message", ex.Message);
            activity.SetTag("aonik.unhandled_exception", true);
            activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
        }

        if (isAllowedCorsOrigin && origin is not null && !context.Response.HasStarted)
        {
            // Re-apply CORS headers and write a 500 response so the browser
            // can read the error instead of blocking it as a CORS failure.
            ApplyActualCorsHeaders(context.Response, origin);

            // Service-layer (and endpoint pre-processor) permission denials
            // are surfaced as the typed PermissionDeniedException carrying
            // the missing permission key. Surfacing them as a generic 500
            // would mislead the client into treating an authorisation
            // failure as a transient outage AND leak the exception type
            // back to the caller. Map them to 403 instead so the front-end
            // (and the logs page) can render an authorisation problem
            // distinctly from a real server error.
            var permissionDenied = ex as Aonik.SharedKernel.Abstractions.PermissionDeniedException;
            var isPermissionDenied = permissionDenied is not null;

            context.Response.StatusCode = isPermissionDenied
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            // In dev / non-prod environments include the exception type
            // and message in the response so the operator can diagnose
            // without round-tripping to App Insights. Production responses
            // stay opaque.
            var includeDetails = app.Environment.IsDevelopment()
                || string.Equals(app.Environment.EnvironmentName, "dev", StringComparison.OrdinalIgnoreCase);
            var topLevelMessage = isPermissionDenied
                ? ex.Message
                : "An internal error occurred.";
            object errorPayload;
            if (isPermissionDenied)
            {
                // Always surface the missing permission key so the front-end
                // can show a precise "you need <Permission>" message in both
                // dev and prod, without leaking other exception details.
                errorPayload = new
                {
                    error = topLevelMessage,
                    permissionKey = permissionDenied!.PermissionKey,
                };
            }
            else if (includeDetails)
            {
                errorPayload = new
                {
                    error = topLevelMessage,
                    exceptionType = ex.GetType().FullName,
                    exceptionMessage = ex.Message,
                    innerType = innermost.GetType().FullName,
                    innerMessage = innermost.Message,
                    exceptionChain = FlattenChain(ex),
                    path = context.Request.Path.Value,
                };
            }
            else
            {
                errorPayload = new { error = topLevelMessage };
            }

            var errorBody = System.Text.Json.JsonSerializer.Serialize(errorPayload);
            await context.Response.WriteAsync(errorBody);
            return; // Do not re-throw — response is already written with CORS headers
        }

        throw;
    }
});

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

// Routing + CORS middleware. The custom CORS middleware (before UseHttpsRedirection)
// handles actual CORS header writing via OnStarting; UseCors is still required so
// ASP.NET Core's EndpointMiddleware does not throw when it finds RequireCors metadata
// on endpoints. The custom middleware's ContainsKey guard prevents duplicate headers.
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

    // Serve AI-generated content media (hero images, etc.)
    var contentMediaPath = builder.Configuration["BlobStorage:ContentMedia:Path"] ?? "content-media";
    var contentMediaPhysicalPath = Path.Combine(Directory.GetCurrentDirectory(), localBasePath, contentMediaPath);
    Directory.CreateDirectory(contentMediaPhysicalPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentMediaPhysicalPath),
        RequestPath = "/storage/content-media",
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

// 6. AI Playground review endpoint (evaluates agent responses with RAGAS-style metrics)
app.MapPlaygroundReview("/ai/playground/review")
    .RequireAuthorization("AdminPolicy")
    .RequireCors("AonikCors");

// 7. AI Playground scenario endpoints (CRUD + AI wizard)
app.MapPlaygroundScenarios("/ai/playground/scenarios")
    .RequireAuthorization("AdminPolicy")
    .RequireCors("AonikCors");

app.MapPlaygroundScenarioGenerate("/ai/playground/scenarios/generate")
    .RequireAuthorization("AdminPolicy")
    .RequireCors("AonikCors");

app.MapAdminNotificationStreaming("/admin/notifications/stream")
    .RequireAuthorization("AdminPolicy")
    .RequireCors("AonikCors");

// Scalar API Reference (OpenAPI UI) — must be after routing/FastEndpoints
app.UseAonikSwagger(builder.Configuration);

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

static void ApplyActualCorsHeaders(HttpResponse response, string origin)
{
    response.Headers["Access-Control-Allow-Origin"] = origin;
    response.Headers["Access-Control-Allow-Credentials"] = "true";

    var varyHeader = response.Headers.Vary.ToString();
    if (string.IsNullOrWhiteSpace(varyHeader))
    {
        response.Headers["Vary"] = "Origin";
        return;
    }

    if (!varyHeader.Contains("Origin", StringComparison.OrdinalIgnoreCase))
    {
        response.Headers.Append("Vary", "Origin");
    }
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

static async Task EnsurePlatformAdminRolePermissionsAsync(PlatformDbContext dbContext, ILogger logger)
{
    var platformAdminRole = await dbContext.Roles
        .FirstOrDefaultAsync(r => r.TenantId == Guid.Empty && r.Name == "PlatformAdmin");

    if (platformAdminRole == null)
    {
        logger.LogInformation("PlatformAdmin role not found — skipping role-permission seed.");
        return;
    }

    var allPermissions = await dbContext.Permissions.ToListAsync();
    if (allPermissions.Count == 0)
    {
        logger.LogWarning("No permissions found in database — skipping PlatformAdmin role-permission seed.");
        return;
    }

    var existingPermissionIds = await dbContext.RolePermissions
        .Where(rp => rp.RoleId == platformAdminRole.Id)
        .Select(rp => rp.PermissionId)
        .ToListAsync();

    var existingSet = new HashSet<Guid>(existingPermissionIds);
    var newMappings = allPermissions
        .Where(p => !existingSet.Contains(p.Id))
        .Select(p => new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = platformAdminRole.Id,
            PermissionId = p.Id
        })
        .ToList();

    if (newMappings.Count == 0)
    {
        logger.LogInformation("PlatformAdmin role already has all {Count} permission mappings.", allPermissions.Count);
        return;
    }

    dbContext.RolePermissions.AddRange(newMappings);
    await dbContext.SaveChangesAsync();
    logger.LogInformation("Seeded {Count} role-permission mappings for PlatformAdmin.", newMappings.Count);
}

/// <summary>
/// Walks every tenant role and tops up the role-permission mapping for any
/// permission that should be granted by default but is currently missing.
/// Mirrors the role→permission dictionary in
/// <c>TenantProvisioner.EnsureDefaultRolePermissionsAsync</c>, but runs on
/// startup so previously-provisioned tenants pick up newly-added permissions
/// (e.g. <c>Catalog.Write</c>) without needing a host operator to re-run
/// provisioning manually. Idempotent — only adds missing rows.
/// </summary>
static async Task EnsureTenantRolePermissionsUpToDateAsync(PlatformDbContext dbContext, ILogger logger)
{
    // Keep this dictionary in sync with TenantProvisioner.
    // EnsureDefaultRolePermissionsAsync. Out-of-band drift is fine for
    // role names that don't exist in this tenant; we just skip them.
    var rolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["TenantAdmin"] = new[]
        {
            "Users.Read", "Users.Invite", "Users.Manage", "Users.Deactivate",
            "UserInfo.Read", "UserInfo.Update",
            "Roles.Read", "Roles.Create", "Roles.Update", "Roles.Delete",
            "Permissions.Read",
            "Settings.Read", "Settings.Write",
            "Ledger.Read", "Ledger.Write", "Ledger.Reconcile",
            "Payment.Read", "Payment.Create", "Payment.Capture", "Payment.Cancel", "Payment.Refund",
            "Invoice.Read", "Invoice.Create", "Invoice.Update", "Invoice.Delete", "Invoice.Issue",
            "Catalog.Read", "Catalog.Write",
            "Customers.Read", "Customers.Create", "Customers.Write"
        },
        ["Operations"] = new[]
        {
            "Ledger.Read", "Ledger.Write", "Ledger.Reconcile",
            "Payment.Read", "Payment.Create", "Payment.Capture", "Payment.Cancel", "Payment.Refund",
            "Invoice.Read", "Invoice.Create", "Invoice.Update", "Invoice.Delete", "Invoice.Issue",
            "Catalog.Read",
            "Customers.Read", "Customers.Create", "Customers.Write"
        },
        ["ReadOnly"] = new[]
        {
            "Users.Read", "UserInfo.Read", "Roles.Read",
            "Settings.Read", "Ledger.Read", "Payment.Read", "Invoice.Read",
            "Catalog.Read", "Customers.Read"
        },
        ["Compliance"] = new[]
        {
            "Users.Read", "Settings.Read", "Ledger.Read",
            "Payment.Read", "Invoice.Read",
            "Catalog.Read", "Customers.Read"
        },
        ["PersonalUser"] = new[]
        {
            "UserInfo.Read", "UserInfo.Update",
            "Settings.Read", "Settings.Write",
            "Catalog.Read"
        }
    };

    var allPermissions = await dbContext.Permissions.ToListAsync();
    if (allPermissions.Count == 0)
    {
        logger.LogInformation("No permissions seeded yet — skipping tenant role-permission top-up.");
        return;
    }

    var permissionLookup = allPermissions.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

    // Tenant-scoped roles only — exclude PlatformAdmin (TenantId == Guid.Empty),
    // which has its own dedicated catch-up routine above.
    var tenantRoles = await dbContext.Roles
        .Where(r => r.TenantId != Guid.Empty)
        .ToListAsync();

    if (tenantRoles.Count == 0)
    {
        logger.LogInformation("No tenant-scoped roles found — skipping tenant role-permission top-up.");
        return;
    }

    var totalAdded = 0;
    foreach (var role in tenantRoles)
    {
        if (!rolePermissions.TryGetValue(role.Name, out var desiredKeys))
            continue;

        var desiredIds = desiredKeys
            .Where(permissionLookup.ContainsKey)
            .Select(k => permissionLookup[k].Id)
            .ToList();

        var existingIds = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var existingSet = new HashSet<Guid>(existingIds);
        var missing = desiredIds.Where(id => !existingSet.Contains(id)).ToList();
        if (missing.Count == 0) continue;

        dbContext.RolePermissions.AddRange(missing.Select(permissionId => new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = permissionId
        }));
        totalAdded += missing.Count;

        logger.LogInformation(
            "Topped up {Count} missing permission mappings on role {RoleName} (TenantId={TenantId}).",
            missing.Count,
            role.Name,
            role.TenantId);
    }

    if (totalAdded > 0)
    {
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Tenant role-permission top-up added {Count} role-permission rows total.", totalAdded);
    }
    else
    {
        logger.LogInformation("Tenant role-permission top-up: all roles already up to date.");
    }
}

// Make the Program class accessible for testing
public partial class Program { }
