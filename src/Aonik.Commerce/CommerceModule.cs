using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Modules;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce;

/// <summary>
/// Composition-root registration for the Commerce module (Spec 042). Phase 1 wires the
/// module-scoped <see cref="CommerceDbContext"/> and the catalog + pricing services. Inventory,
/// cart/checkout, the billing/payment write contracts, and the commerce-agent land in subsequent
/// phases. The canonical migration stream stays in <c>AonikDbContext</c>.
/// </summary>
public sealed class CommerceModule : IModule
{
    public static string Name => "Commerce";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CommerceDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"CommerceDb_{Guid.NewGuid()}";
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

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductPricingService, ProductPricingService>();

        return services;
    }
}

public static class CommerceModuleExtensions
{
    public static IServiceCollection AddCommerceModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => CommerceModule.ConfigureServices(services, configuration);
}
