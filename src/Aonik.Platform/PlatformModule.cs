using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform;

/// <summary>
/// Platform module registration. Owns Identity, Tenancy, Party/Profile,
/// Compliance, Notifications, and Operations domains.
/// </summary>
public sealed class PlatformModule : IModule
{
    public static string Name => "Platform";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register PlatformDbContext
        // Shares the same physical database as the monolithic AonikDbContext.
        // Uses the 'platform' schema for logical isolation.
        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"PlatformDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString);
            }
        });

        // Platform services will be registered here as entities and services
        // are migrated from the monolithic Application/Infrastructure layers.

        return services;
    }
}

/// <summary>
/// Extension methods for registering the Platform module in the DI container.
/// </summary>
public static class PlatformModuleExtensions
{
    /// <summary>
    /// Adds the Platform module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddPlatformModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => PlatformModule.ConfigureServices(services, configuration);
}
