using System.Data.Common;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Seeding;
using Aonik.Finance.Services.Seeding;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Api.Configuration;

/// <summary>
/// Startup-time database concerns: connection diagnostics, migrations,
/// and seed routines.
/// </summary>
/// <remarks>
/// Extracted from <c>Program.cs</c> as part of slimming the composition
/// root. Behaviour is unchanged from the previous inline implementation —
/// only the file boundaries have moved.
///
/// <para>
/// All migrations flow through the canonical <see cref="AonikDbContext"/>;
/// module-scoped DbContexts share the same physical database but do not
/// maintain independent migration histories. <see cref="DiscoverMigratableDbContexts"/>
/// is the single source of truth for that rule.
/// </para>
/// </remarks>
public static class DatabaseStartupConfiguration
{
    /// <summary>
    /// Logs the resolved Aonik database connection (server, database, auth
    /// mode) at startup so an operator can confirm at-a-glance which DB the
    /// API is talking to.
    /// </summary>
    public static void LogResolvedDatabaseConnection(this IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<AonikDbContext>>();

        // AonikDbContext is scoped; resolving it from the root provider throws under Development
        // scope-validation ("Cannot resolve scoped service ... from root provider"). Open a scope
        // for this startup diagnostic so the API can boot in Development.
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AonikDbContext>();

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

    /// <summary>
    /// Migrates and (optionally) seeds the database. Both gates are off by
    /// default; the API auto-runs them in <c>Development</c>, and either
    /// can be forced on via <c>Database:AutoMigrate</c> /
    /// <c>Database:SeedData</c> configuration.
    /// </summary>
    /// <remarks>
    /// Migration or seed failures are fatal by default: the process exits
    /// non-zero so orchestration can halt the rollout and prevent traffic
    /// from hitting a half-migrated schema. Set <c>Database:AllowDegradedStart</c>
    /// to <c>true</c> to opt in to the old swallow-and-continue behaviour
    /// (e.g. a local dev box where the database is intentionally offline).
    /// </remarks>
    public static async Task InitializeAonikDatabaseAsync(this WebApplication app)
    {
        var autoMigrateEnabled = app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Database:AutoMigrate");
        var seedDataEnabled = app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Database:SeedData");

        if (!autoMigrateEnabled && !seedDataEnabled)
        {
            return;
        }

        var allowDegradedStart = app.Configuration.GetValue<bool>("Database:AllowDegradedStart");

        using var scope = app.Services.CreateScope();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var platformDbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        try
        {
            if (autoMigrateEnabled)
            {
                await ApplyMigrationsAsync(scope.ServiceProvider, startupLogger);
            }

            if (seedDataEnabled)
            {
                await RunSeedRoutinesAsync(scope.ServiceProvider, platformDbContext, startupLogger);
            }
        }
        catch (Exception ex) when (allowDegradedStart)
        {
            startupLogger.LogWarning(ex,
                "Database initialization failed; continuing because Database:AllowDegradedStart is set. " +
                "The API may behave incorrectly against a partial schema.");
        }
    }

    private static async Task ApplyMigrationsAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Running database migrations...");

        foreach (var dbContextType in DiscoverMigratableDbContexts())
        {
            var dbContext = (DbContext)services.GetRequiredService(dbContextType);
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

            if (!pendingMigrations.Any())
            {
                logger.LogInformation("No pending migrations for {DbContext}.", dbContextType.Name);
                continue;
            }

            logger.LogInformation(
                "Applying {Count} pending migration(s) for {DbContext}...",
                pendingMigrations.Count(),
                dbContextType.Name);

            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations for {DbContext} completed successfully.", dbContextType.Name);
        }

        logger.LogInformation("Database migrations completed successfully.");
    }

    private static async Task RunSeedRoutinesAsync(
        IServiceProvider services,
        PlatformDbContext platformDbContext,
        ILogger startupLogger)
    {
        await new IdentitySeedService(
                platformDbContext,
                services.GetRequiredService<ILogger<IdentitySeedService>>())
            .SeedAsync();

        // IdentitySeedService only seeds Permission rows; if the platform
        // was bootstrapped before SeedData was enabled, the PlatformAdmin
        // role exists but has zero RolePermission records, blocking all
        // API calls. Top those up here.
        await EnsurePlatformAdminRolePermissionsAsync(platformDbContext, startupLogger);

        // Top up tenant TenantAdmin/Operations/etc. roles with permissions
        // added since the tenant was first provisioned.
        // EnsureDefaultRolePermissionsAsync (in TenantProvisioner) only
        // runs at provisioning time, so a tenant created before a new
        // permission was added (e.g. Catalog.Write) ends up missing it.
        // This pass walks every tenant once per startup and inserts any
        // missing role-permission rows.
        await EnsureTenantRolePermissionsUpToDateAsync(platformDbContext, startupLogger);

        await new CatalogSeedService(
                platformDbContext,
                services.GetRequiredService<ILogger<CatalogSeedService>>())
            .SeedAsync();

        await new SettingsSeedService(
                platformDbContext,
                services.GetRequiredService<ILogger<SettingsSeedService>>())
            .SeedAsync();

        await new NotificationTemplateSeedService(
                platformDbContext,
                services.GetRequiredService<ILogger<NotificationTemplateSeedService>>())
            .SeedAsync();

        var aiDbContext = services.GetRequiredService<AiDbContext>();

        await new AiTaskSeedService(
                aiDbContext,
                services.GetRequiredService<ILogger<AiTaskSeedService>>())
            .SeedAsync();

        var financePricingSeed = services.GetRequiredService<FinancePricingSeedContributor>();
        var financePricingOperations = await financePricingSeed.SeedAsync();
        startupLogger.LogInformation(
            "Global seed {SeedKey} completed with {OperationCount} operation(s).",
            financePricingSeed.Key,
            financePricingOperations.Count);

        startupLogger.LogInformation("Database seed routines completed successfully.");
    }

    /// <summary>
    /// The set of DbContexts whose pending migrations should be applied at
    /// startup. Today only <see cref="AonikDbContext"/>: per the project
    /// rules, all migrations flow through it, and module-scoped contexts
    /// share the same physical database without their own migration
    /// histories.
    /// </summary>
    private static IReadOnlyList<Type> DiscoverMigratableDbContexts() =>
        [typeof(AonikDbContext)];

    private static (string Server, string Database, string Authentication) ParseConnectionInfo(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        var server = GetConnectionValue(builder, "Data Source", "Server", "Address", "Addr", "Network Address")
            ?? "(unknown)";
        var database = GetConnectionValue(builder, "Initial Catalog", "Database")
            ?? "(unknown)";

        var integratedSecurityValue = GetConnectionValue(builder, "Integrated Security", "Trusted_Connection");
        var isIntegratedSecurity = IsTrue(integratedSecurityValue)
            || string.Equals(integratedSecurityValue, "SSPI", StringComparison.OrdinalIgnoreCase);

        var authentication = isIntegratedSecurity ? "IntegratedSecurity" : "SqlAuth";

        return (server, database, authentication);
    }

    private static string? GetConnectionValue(DbConnectionStringBuilder builder, params string[] keys)
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

    private static bool IsTrue(string? value)
        => value is not null
        && (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds any missing <c>RolePermission</c> rows to the global
    /// PlatformAdmin role. Idempotent — only inserts rows that don't
    /// already exist.
    /// </summary>
    private static async Task EnsurePlatformAdminRolePermissionsAsync(
        PlatformDbContext dbContext,
        ILogger logger)
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
            logger.LogInformation(
                "PlatformAdmin role already has all {Count} permission mappings.",
                allPermissions.Count);
            return;
        }

        dbContext.RolePermissions.AddRange(newMappings);
        await dbContext.SaveChangesAsync();
        logger.LogInformation(
            "Seeded {Count} role-permission mappings for PlatformAdmin.",
            newMappings.Count);
    }

    /// <summary>
    /// Walks every tenant role and tops up the role-permission mapping for
    /// any permission that should be granted by default but is currently
    /// missing. Mirrors the role→permission dictionary in
    /// <c>TenantProvisioner.EnsureDefaultRolePermissionsAsync</c>, but runs
    /// on startup so previously-provisioned tenants pick up newly-added
    /// permissions (e.g. <c>Catalog.Write</c>) without needing a host
    /// operator to re-run provisioning manually. Idempotent.
    /// </summary>
    private static async Task EnsureTenantRolePermissionsUpToDateAsync(
        PlatformDbContext dbContext,
        ILogger logger)
    {
        // Keep this dictionary in sync with TenantProvisioner.
        // EnsureDefaultRolePermissionsAsync. Out-of-band drift is fine
        // for role names that don't exist in this tenant; they're skipped.
        var rolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["TenantAdmin"] =
            [
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
            ],
            ["Operations"] =
            [
                "Ledger.Read", "Ledger.Write", "Ledger.Reconcile",
                "Payment.Read", "Payment.Create", "Payment.Capture", "Payment.Cancel", "Payment.Refund",
                "Invoice.Read", "Invoice.Create", "Invoice.Update", "Invoice.Delete", "Invoice.Issue",
                "Catalog.Read",
                "Customers.Read", "Customers.Create", "Customers.Write"
            ],
            ["ReadOnly"] =
            [
                "Users.Read", "UserInfo.Read", "Roles.Read",
                "Settings.Read", "Ledger.Read", "Payment.Read", "Invoice.Read",
                "Catalog.Read", "Customers.Read"
            ],
            ["Compliance"] =
            [
                "Users.Read", "Settings.Read", "Ledger.Read",
                "Payment.Read", "Invoice.Read",
                "Catalog.Read", "Customers.Read"
            ],
            ["PersonalUser"] =
            [
                "UserInfo.Read", "UserInfo.Update",
                "Settings.Read", "Settings.Write",
                "Catalog.Read"
            ]
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
            logger.LogInformation(
                "Tenant role-permission top-up added {Count} role-permission rows total.",
                totalAdded);
        }
        else
        {
            logger.LogInformation("Tenant role-permission top-up: all roles already up to date.");
        }
    }
}
