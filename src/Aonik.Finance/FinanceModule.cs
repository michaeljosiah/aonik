using Aonik.Agents.Framework;
using Aonik.Finance.Agents;
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

        // Payments
        services.AddScoped<Contracts.Services.Payments.IPaymentService, Services.Payments.PaymentService>();
        services.AddScoped<Contracts.Services.Payments.IPublicPaymentService, Services.Payments.PublicPaymentService>();
        services.AddSingleton<Contracts.Services.Payments.IPaymentProviderGateway, Services.Payments.StripeSimulatedPaymentProviderGateway>();

        // Billing
        services.AddScoped<Contracts.Services.Billing.IBillingService, Services.Billing.BillingService>();

        // Orders
        services.AddScoped<Contracts.Services.Orders.IOrderService, Services.Orders.OrderService>();
        services.AddScoped<Contracts.Services.Orders.IPublicOrderService, Services.Orders.PublicOrderService>();
        services.AddScoped<SharedKernel.Abstractions.IOrderExistenceChecker, Services.Orders.OrderExistenceChecker>();
        services.AddScoped<SharedKernel.Abstractions.ICustomerFinanceStatsProvider, Services.Orders.CustomerFinanceStatsProvider>();

        // Cross-module provisioning contributor
        services.AddScoped<SharedKernel.Abstractions.ITenantProvisioningContributor, Services.Provisioning.FinanceTenantProvisioningContributor>();

        // Cross-module demo-seed contributor
        services.AddScoped<SharedKernel.Abstractions.IDemoSeedContributor, Services.Seeding.FinanceDemoSeedContributor>();

        // Pricing
        services.AddScoped<Contracts.Services.Pricing.IPricingService, Services.Pricing.PricingService>();
        services.AddScoped<Contracts.Services.Pricing.IPricingPolicyService, Services.Pricing.PricingPolicyService>();
        services.AddScoped<Contracts.Services.Pricing.IFxRateService, Services.Pricing.FxRateService>();
        services.AddScoped<Contracts.Services.Pricing.IFxQuoteService, Services.Pricing.FxQuoteService>();
        services.AddSingleton<SharedKernel.Abstractions.ICurrencyMetadataProvider, Services.Pricing.CurrencyMetadataProvider>();

        // Partners
        services.AddScoped<Contracts.Services.Partners.IPartnerAdminService, Services.Partners.PartnerAdminService>();

        // Catalog
        services.AddScoped<Contracts.Services.Catalog.ICatalogService, Services.Catalog.CatalogService>();
        services.AddScoped<Contracts.Services.Catalog.IPublicCatalogService, Services.Catalog.PublicCatalogService>();

        // PersonalFinance
        services.AddScoped<Contracts.Services.PersonalFinance.IHouseholdService, Services.PersonalFinance.HouseholdService>();

        // ── Finance AI Insights ──────────────────────────────────────
        services.AddScoped<Services.Ai.InvoiceInsightWorkflow>();
        services.AddScoped<Contracts.Services.Ai.IFinanceInsightsService, Services.Ai.FinanceInsightsService>();

        // ── Finance Domain Agent ─────────────────────────────────────
        services.AddSingleton<AonikDomainAgent, FinanceDomainAgent>();

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
