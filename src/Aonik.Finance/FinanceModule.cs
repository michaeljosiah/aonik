using Aonik.Finance.Agents;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Events;
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

        // ── Event Bus ────────────────────────────────────────────────
        services.AddEventBus(typeof(FinanceModule).Assembly);

        // ── Observability ─────────────────────────────────────────────
        // Domain-event counters (payments / invoices / ledger entries) on the
        // "Aonik.Finance" meter; subscribed in ServiceDefaults.
        services.AddSingleton<Services.Observability.FinanceMetrics>();

        // ── Finance Services ─────────────────────────────────────────
        // Ledger
        services.AddScoped<Contracts.Services.Ledger.ILedgerService, Services.Ledger.LedgerService>();
        // System-initiated double-entry poster for payment capture / invoice
        // settlement. Writes JournalEntry rows directly (no Ledger.Write gate),
        // mirroring the PartnerPrefundSeedHelper direct-write pattern.
        services.AddScoped<Services.Ledger.LedgerPostingService>();

        // Payments
        services.AddScoped<Contracts.Services.Payments.IPaymentService, Services.Payments.PaymentService>();
        services.AddScoped<Contracts.Services.Payments.IPublicPaymentService, Services.Payments.PublicPaymentService>();
        services.AddSingleton<Contracts.Services.Payments.IPaymentProviderGateway, Services.Payments.StripeSimulatedPaymentProviderGateway>();

        // Pay Activity (mobile BFF)
        services.AddScoped<Contracts.Services.PayActivity.IPayActivityService, Services.PayActivity.PayActivityService>();

        // Billing
        services.AddScoped<Contracts.Services.Billing.IBillingService, Services.Billing.BillingService>();

        // Insights
        services.AddScoped<Contracts.Services.Insights.IMySpaceSummaryService, Services.Insights.MySpaceSummaryService>();

        // Orders
        services.AddScoped<Contracts.Services.Orders.IOrderService, Services.Orders.OrderService>();
        services.AddScoped<Contracts.Services.Orders.IPublicOrderService, Services.Orders.PublicOrderService>();
        services.AddScoped<SharedKernel.Abstractions.IOrderExistenceChecker, Services.Orders.OrderExistenceChecker>();
        services.AddScoped<SharedKernel.Abstractions.ICustomerFinanceStatsProvider, Services.Orders.CustomerFinanceStatsProvider>();
        services.AddScoped<SharedKernel.Abstractions.ICustomerActivityProvider, Services.Orders.CustomerActivityProvider>();

        // ── Cross-Module Read Contracts (Spec 027 boundary) ─────────
        // These thin readers let PersonalFinance (and other consumers)
        // read Order / Invoice / Payment data without taking a project
        // reference on Aonik.Finance.Entities.*. They are the load-bearing
        // contract that lets the PF extraction land cleanly.
        services.AddScoped<SharedKernel.Abstractions.Finance.ICustomerOrderHistoryReader, Services.Finance.Readers.CustomerOrderHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.ICustomerInvoiceHistoryReader, Services.Finance.Readers.CustomerInvoiceHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.ICustomerPaymentHistoryReader, Services.Finance.Readers.CustomerPaymentHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.IFxQuoteReader, Services.Finance.Readers.FxQuoteReader>();

        // ICustomerDataExportProvider and ICustomerDataImportConsumer
        // relocated to PersonalFinanceModule (Spec 027 Phase 3).

        // Cross-module provisioning contributor
        services.AddScoped<SharedKernel.Abstractions.ITenantProvisioningContributor, Services.Provisioning.FinanceTenantProvisioningContributor>();

        // Cross-module demo-seed contributor + per-phase helpers
        services.AddScoped<Services.Seeding.Phases.PartnerPrefundSeedHelper>();
        services.AddScoped<Services.Seeding.Phases.CatalogUpsertHelper>();
        services.AddScoped<Services.Seeding.Phases.PricingUpsertHelper>();
        services.AddScoped<Services.Seeding.Phases.CatalogCategoriesSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.BillCollectionPartnerSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.CatalogSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.PricingSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.CrossBorderPartnerNetworkSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.CrossBorderCatalogSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.HouseholdsSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.CrossBorderPricingSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.OrderActivitySeedPhase>();
        services.AddScoped<Services.Seeding.Phases.PersonalFinanceActivitySeedPhase>();

        // IPersonalFinancePartyResolver relocated to PersonalFinanceModule (Spec 027 Phase 3).
        services.AddScoped<SharedKernel.Abstractions.IDemoSeedContributor, Services.Seeding.FinanceDemoSeedContributor>();

        // Pricing
        services.AddScoped<Contracts.Services.Pricing.IPricingService, Services.Pricing.PricingService>();
        services.AddScoped<Contracts.Services.Pricing.IPricingPolicyService, Services.Pricing.PricingPolicyService>();
        services.AddScoped<Contracts.Services.Pricing.IFxRateService, Services.Pricing.FxRateService>();
        services.AddScoped<Contracts.Services.Pricing.IFxQuoteService, Services.Pricing.FxQuoteService>();
        services.AddSingleton<SharedKernel.Abstractions.ICurrencyMetadataProvider, Services.Pricing.CurrencyMetadataProvider>();

        // Partners
        services.AddScoped<Contracts.Services.Partners.IPartnerAdminService, Services.Partners.PartnerAdminService>();

        // ── Partner Connectors (Spec 031) ───────────────────────────
        // Partner-agnostic money-movement ports (payout / collection / bill payment + airtime).
        // One concrete simulated connector backs all three ports; forwarding registrations keep
        // it a single object while satisfying each port list the resolver injects. A real vendor
        // is added by registering one more connector against the relevant port(s) — no other change.
        services.AddSingleton<Services.Partners.Connectors.SimulatedPartnerConnector>();
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerPayoutConnector>(
            sp => sp.GetRequiredService<Services.Partners.Connectors.SimulatedPartnerConnector>());
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerCollectionConnector>(
            sp => sp.GetRequiredService<Services.Partners.Connectors.SimulatedPartnerConnector>());
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerBillPaymentConnector>(
            sp => sp.GetRequiredService<Services.Partners.Connectors.SimulatedPartnerConnector>());
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerWebhookTranslator,
            Services.Partners.Connectors.SimulatedPartnerWebhookTranslator>();
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerConnectorResolver,
            Services.Partners.Connectors.PartnerConnectorResolver>();

        // Catalog
        services.AddScoped<Contracts.Services.Catalog.ICatalogService, Services.Catalog.CatalogService>();
        services.AddScoped<Contracts.Services.Catalog.IPublicCatalogService, Services.Catalog.PublicCatalogService>();

        // PersonalFinance
        // IBillService, IDashboardService, IHouseholdService, IPersonalAccountService,
        // IPersonalAccountLinkService, IPersonalTransactionService, IStatementImportService,
        // ITransactionClassificationService, IPersonalFinanceInsightsService,
        // FinancialConnectionTransactionSyncOrchestrator, ICustomerInsightSnapshotGenerator,
        // ICustomerInsightSnapshotService, ICustomerInsightSnapshotReader, and
        // ICustomerInsightSnapshotForAi all relocated to PersonalFinanceModule
        // (Spec 027 Phase 3 + Phase 7 deferred-refactor wrap-up).
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

        services.AddScoped<Contracts.Services.PersonalFinance.ITransactionAiClassifier, Services.PersonalFinance.TransactionAiClassifier>();
        services.AddScoped<Contracts.Services.PersonalFinance.IPersonalFinanceNarrativeInsightsService, Services.PersonalFinance.PersonalFinanceNarrativeInsightsService>();
        // The entire FinancialLifeGraph cluster (Schema, Loader, SnapshotMetrics,
        // HydrationService, Service, SchemaService, TraversalService,
        // CacheInvalidator, ValidationService, WriteService, InferenceService,
        // RetrievalService) has been relocated to PersonalFinanceModule
        // (Spec 027 Phase 3 + Phase 7 deferred-refactor wrap-up).
        // IFinancialContextService likewise relocated to PersonalFinanceModule.

        // Cross-module IPersonalProfileProvisioner and IUserBriefDataProvider
        // relocated to PersonalFinanceModule (Spec 027 Phase 3).

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
        services.AddScoped<Services.Accounts.IAccountTransactionCategorizer,
            Services.Accounts.AccountTransactionCategorizer>();
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

        // Spec 025 — three analytical sub-agents Simi invokes via the
        // pf_run_insights / pf_run_forecast / pf_run_classify_review tools.
        // Replaced the legacy pf-spending-intelligence + pf-obligation-planning
        // sub-agents in Phase 6 (their descriptor / tool / structured-output
        // files were deleted at the same time as this DI list shrunk).
        services.AddSingleton<IDomainAgentDescriptor, PfInsightsAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfForecastAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfClassifyAgentDescriptor>();

        // CodeAct sandbox providers and selector relocated to PersonalFinanceModule
        // (Spec 027 Phase 5) along with the CodeAct/* file tree.

        // ── Global Seed Contributors ────────────────────────────────────
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.PersonalFinanceSeedContributor>();

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
