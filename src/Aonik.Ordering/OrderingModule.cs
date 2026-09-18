using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Modules;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Ordering;

/// <summary>
/// Composition-root registration for the Ordering module (Spec 041 / ADR-011). Wires the
/// module-scoped <see cref="OrderingDbContext"/> and the core <see cref="IOrderService"/> over the
/// generic Order spine. The canonical migration stream stays in <c>AonikDbContext</c>; this
/// context declares no migrations. Domain modules (Finance, future Commerce) compose
/// <see cref="IOrderService"/> for their type-specific order creation.
/// </summary>
public sealed class OrderingModule : IModule
{
    public static string Name => "Ordering";
    public static string Id => ModuleIds.Ordering;

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OrderingDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"OrderingDb_{Guid.NewGuid()}";
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

        // The core, type-agnostic Order spine, exposed through the SharedKernel contract.
        services.AddScoped<IOrderService, CoreOrderService>();

        return services;
    }
}

public static class OrderingModuleExtensions
{
    public static IServiceCollection AddOrderingModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => OrderingModule.ConfigureServices(services, configuration);
}
