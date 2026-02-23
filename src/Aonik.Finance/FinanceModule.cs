using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance;

/// <summary>
/// Finance module registration. Owns Ledger, Payments, Billing, Orders,
/// Pricing, Partners, and PersonalFinance domains.
/// </summary>
public sealed class FinanceModule : IModule
{
    public static string Name => "Finance";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register FinanceDbContext
        // Shares the same physical database as AonikDbContext and PlatformDbContext.
        // Uses the 'finance' schema for logical isolation.
        services.AddDbContext<FinanceDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"FinanceDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString);
            }
        });

        // ── Finance Services ─────────────────────────────────────────
        // Ledger
        services.AddScoped<Contracts.Services.Ledger.ILedgerService, Services.Ledger.LedgerService>();

        return services;
    }
}

/// <summary>
/// Extension methods for registering the Finance module in the DI container.
/// </summary>
public static class FinanceModuleExtensions
{
    /// <summary>
    /// Adds the Finance module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddFinanceModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => FinanceModule.ConfigureServices(services, configuration);
}
