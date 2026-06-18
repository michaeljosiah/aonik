using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Events;
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
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();

        // Spec 042 §11 — react to PaymentCompletedEvent (commit inventory, close cart, complete
        // order). The outbox dispatcher in the Worker invokes these with the tenant restored.
        services.AddEventHandlersFromAssembly(typeof(CommerceModule).Assembly);

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
