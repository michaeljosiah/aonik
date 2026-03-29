using Aonik.Agents.Contracts.Services;
using Aonik.Finance.Agents;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        // Shares the same physical database as AonikDbContext and PlatformDbContext
        // using dbo schema + module table prefixes.
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
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.EnableRetryOnFailure());
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
        services.AddScoped<Contracts.Services.PersonalFinance.IBillService, Services.PersonalFinance.BillService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IDashboardService, Services.PersonalFinance.DashboardService>();
        services.Configure<Services.PersonalFinance.PlaidAccountLinkOptions>(
            configuration.GetSection("Finance:PersonalFinance:Plaid"));
        services.Configure<Services.PersonalFinance.FinancialConnectionSyncOptions>(
            configuration.GetSection("Finance:PersonalFinance:LinkedAccountSync"));
        services.AddHttpClient<Services.PersonalFinance.PlaidAccountLinkProviderGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<Services.PersonalFinance.PlaidAccountLinkOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddScoped<Contracts.Services.PersonalFinance.IHouseholdService, Services.PersonalFinance.HouseholdService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalAccountService, Services.PersonalFinance.PersonalAccountService>();
        services.AddScoped<Services.PersonalFinance.FinancialConnectionTransactionSyncOrchestrator>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalAccountLinkService, Services.PersonalFinance.PersonalAccountLinkService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalTransactionService, Services.PersonalFinance.PersonalTransactionService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IStatementImportService, Services.PersonalFinance.StatementImportService>();
        services.AddScoped<Contracts.Services.PersonalFinance.ITransactionClassificationService, Services.PersonalFinance.TransactionClassificationService>();
        services.AddScoped<Contracts.Services.PersonalFinance.ITransactionAiClassifier, Services.PersonalFinance.TransactionAiClassifier>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalFinanceInsightsService, Services.PersonalFinance.PersonalFinanceInsightsService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalFinanceNarrativeInsightsService, Services.PersonalFinance.PersonalFinanceNarrativeInsightsService>();
        services.AddSingleton<Services.PersonalFinance.FinancialLifeGraphSchema>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphLoader>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphSnapshotMetrics>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphHydrationService>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IFinancialLifeGraphService>(sp => sp.GetRequiredService<Services.PersonalFinance.FinancialLifeGraphService>());
        services.AddScoped<Services.PersonalFinance.IFinancialLifeGraphCacheInvalidator, Services.PersonalFinance.FinancialLifeGraphCacheInvalidator>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphValidationService>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphWriteService>();
        services.AddScoped<Services.PersonalFinance.FinancialLifeGraphInferenceService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IFinancialLifeGraphSchemaService, Services.PersonalFinance.FinancialLifeGraphSchemaService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IFinancialLifeGraphTraversalService, Services.PersonalFinance.FinancialLifeGraphTraversalService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IFinancialLifeGraphRetrievalService, Services.PersonalFinance.FinancialLifeGraphRetrievalService>();
        services.AddScoped<Contracts.Services.PersonalFinance.ITransactionAttachmentService, Services.PersonalFinance.TransactionAttachmentService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IFinancialContextService, Services.PersonalFinance.FinancialContextService>();
        services.AddScoped<Contracts.Services.PersonalFinance.IBudgetService, Services.PersonalFinance.BudgetService>();
        services.AddTransient<Contracts.Services.PersonalFinance.IPersonalAccountLinkProviderGateway>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<Services.PersonalFinance.PlaidAccountLinkOptions>>().Value;
            if (options.IsConfigured())
            {
                return sp.GetRequiredService<Services.PersonalFinance.PlaidAccountLinkProviderGateway>();
            }

            return new Services.PersonalFinance.PlaidSimulatedAccountLinkProviderGateway();
        });

        // Accounts (Tenant-Scoped Bank Linking)
        services.Configure<Services.Accounts.AccountConnectionSyncOptions>(
            configuration.GetSection("Finance:Accounts:LinkedAccountSync"));
        services.AddScoped<Services.Accounts.AccountTransactionSyncOrchestrator>();
        services.AddScoped<Contracts.Services.Accounts.IAccountLinkService,
            Services.Accounts.AccountLinkService>();

        // ── Finance AI Insights ──────────────────────────────────────
        services.AddScoped<Services.Ai.InvoiceInsightWorkflow>();
        services.AddScoped<Contracts.Services.Ai.IFinanceInsightsService, Services.Ai.FinanceInsightsService>();

        // ── Finance Domain Agents ────────────────────────────────────
        // Registered as IDomainAgentDescriptor for the orchestrator to discover.
        // Finance is split into two sub-agents for better LLM tool selection (R7).
        services.AddSingleton<IDomainAgentDescriptor, FinanceAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, FinancialLifeGraphAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PersonalFinanceAgentDescriptor>();

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
