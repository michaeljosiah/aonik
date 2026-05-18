using Aonik.Finance.Agents;
using Aonik.Finance.Agents.CodeAct;
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

        // Cross-module customer data export/import
        services.AddScoped<SharedKernel.Abstractions.ICustomerDataExportProvider, Services.PersonalFinance.CustomerDataExportProvider>();
        services.AddScoped<SharedKernel.Abstractions.ICustomerDataImportConsumer, Services.PersonalFinance.CustomerDataImportConsumer>();

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

        // Playground User Brief party→user fallback (demo personas with no UserParty link).
        services.AddScoped<SharedKernel.Abstractions.UserBrief.IPersonalFinancePartyResolver, Services.PersonalFinance.PersonalFinancePartyResolver>();
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
        services.AddScoped<Contracts.Services.PersonalFinance.ICustomerInsightSnapshotGenerator, Services.PersonalFinance.CustomerInsightSnapshotGenerator>();
        services.AddScoped<Contracts.Services.PersonalFinance.ICustomerInsightSnapshotService, Services.PersonalFinance.CustomerInsightSnapshotService>();
        services.AddScoped<Contracts.Services.PersonalFinance.ICustomerInsightSnapshotReader, Services.PersonalFinance.CustomerInsightSnapshotReader>();
        // SharedKernel-shaped wrapper consumed by Aonik.Ai's CustomerInsightAiSummaryService
        // — keeps Ai free of a back-pointing reference on Finance.
        services.AddScoped<SharedKernel.Abstractions.PersonalFinance.ICustomerInsightSnapshotForAi, Services.PersonalFinance.CustomerInsightSnapshotForAiAdapter>();
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
        services.AddScoped<Contracts.Services.PersonalFinance.ICommitmentService, Services.PersonalFinance.CommitmentService>();

        // Cross-module personal profile provisioner (used by Platform registration flow)
        services.AddScoped<SharedKernel.Abstractions.PersonalFinance.IPersonalProfileProvisioner, Services.PersonalFinance.PersonalProfileProvisioner>();

        // Cross-module data provider for the UserBriefProjector (Agents module)
        services.AddScoped<SharedKernel.Abstractions.PersonalFinance.IUserBriefDataProvider, Services.PersonalFinance.UserBriefDataProvider>();

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

        // Spec 025 — three analytical sub-agents Simi invokes via the
        // pf_run_insights / pf_run_forecast / pf_run_classify_review tools.
        // Replaced the legacy pf-spending-intelligence + pf-obligation-planning
        // sub-agents in Phase 6 (their descriptor / tool / structured-output
        // files were deleted at the same time as this DI list shrunk).
        services.AddSingleton<IDomainAgentDescriptor, PfInsightsAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfForecastAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfClassifyAgentDescriptor>();

        // ── CodeAct Sandbox Providers ────────────────────────────────
        // Backs the wrapping `execute_code` AIFunction the three sub-agents
        // surface to the LLM. The provider selector reads Ai:CodeAct:Provider
        // and returns Hyperlight (local Linux dev), AcaSessions (cloud), or
        // Null (forces tool-loop fallback for diagnostics / kill-switch).
        services.AddOptions<AcaSessionsOptions>().BindConfiguration(AcaSessionsOptions.SectionName);
        services.AddSingleton<CodeActCallbackNonceService>(sp =>
            new CodeActCallbackNonceService(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CodeActCallbackNonceService>>()));
        services.AddHttpClient<AcaSessionsClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AcaSessionsOptions>>().Value;
            if (Uri.TryCreate(opts.PoolManagementEndpoint, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
            // 240 s > ACA's 220 s per-execution cap, so the upstream timeout
            // surfaces as the user-facing error rather than ours.
            client.Timeout = TimeSpan.FromSeconds(240);
        });
        services.AddSingleton<HyperlightCodeActSandboxProvider>();
        services.AddSingleton<AcaSessionsCodeActSandboxProvider>();
        services.AddSingleton<NullCodeActSandboxProvider>();
        services.AddSingleton<ICodeActSandboxProvider>(sp =>
        {
            var providerName = sp.GetRequiredService<IConfiguration>()["Ai:CodeAct:Provider"];
            return providerName switch
            {
                "AcaSessions" => sp.GetRequiredService<AcaSessionsCodeActSandboxProvider>(),
                "Hyperlight"  => sp.GetRequiredService<HyperlightCodeActSandboxProvider>(),
                _             => sp.GetRequiredService<NullCodeActSandboxProvider>(),
            };
        });

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
