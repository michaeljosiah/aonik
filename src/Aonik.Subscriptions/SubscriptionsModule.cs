using Aonik.SharedKernel.Modules;
using Aonik.Subscriptions.Contracts.Services;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Catalogue;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Subscriptions;

/// <summary>
/// Composition-root registration for the Subscriptions module (Spec 087). Wires the module-scoped
/// <see cref="SubscriptionsDbContext"/> and the catalogue service. The canonical migration stream
/// stays in <c>AonikDbContext</c>; this context declares no migrations.
///
/// Spec 087 P2 registers the catalogue only. The subscription lifecycle, grants and metering
/// (<c>ISubscriptionService</c>, <c>IEntitlementReader</c>, <c>IUsageMeter</c>) land in P3–P4, and
/// the money paths in P5–P6 once Spec 088 has delivered the Finance contracts they need.
/// </summary>
public sealed class SubscriptionsModule : IModule
{
    public static string Name => "Subscriptions";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SubscriptionsDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"SubscriptionsDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure());
            }
        });

        services.AddScoped<ICatalogueService, CatalogueService>();

        return services;
    }
}

public static class SubscriptionsModuleExtensions
{
    public static IServiceCollection AddSubscriptionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => SubscriptionsModule.ConfigureServices(services, configuration);
}
