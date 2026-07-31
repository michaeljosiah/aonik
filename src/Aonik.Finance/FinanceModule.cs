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
/// Pricing, and Partners. PersonalFinance is a separate sibling module
/// (ADR-006 / Spec 027) with its own registration.
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

        // Spec 088 P1 - cross-module ledger write path. ILedgerService stays Finance-internal;
        // these are the SharedKernel-facing mirrors, alongside IInvoiceWriter/IPaymentInitiator.
        services.AddScoped<SharedKernel.Abstractions.Ledgers.IJournalWriter, Services.Ledger.JournalWriter>();
        services.AddScoped<SharedKernel.Abstractions.Ledgers.ILedgerResolver, Services.Ledger.LedgerResolver>();

        // Payments
        services.AddScoped<Contracts.Services.Payments.IPaymentService, Services.Payments.PaymentService>();
        services.AddScoped<Contracts.Services.Payments.IPublicPaymentService, Services.Payments.PublicPaymentService>();
        services.AddSingleton<Contracts.Services.Payments.IPaymentProviderGateway, Services.Payments.StripeSimulatedPaymentProviderGateway>();
        // Saves payout destinations and stitches the customer→recipient party graph
        // (relationship edge + Beneficiary role) via the cross-module IPartyService seam.
        services.AddScoped<Contracts.Services.Payments.IPayoutBeneficiaryService, Services.Payments.PayoutBeneficiaryService>();
        // Spec 008 — customer-facing recipient surface (CRUD + photo) projected over the
        // payout-beneficiary party graph. A façade: no recipient table of its own.
        services.AddScoped<Contracts.Services.Payments.IRecipientService, Services.Payments.RecipientService>();
        // Spec 007 — customer card vault. Token-only (gateway token + masked metadata); no PCI data stored.
        services.AddScoped<Contracts.Services.Payments.IPaymentMethodService, Services.Payments.PaymentMethodService>();

        // Pay Activity (mobile BFF)
        services.AddScoped<Contracts.Services.PayActivity.IPayActivityService, Services.PayActivity.PayActivityService>();

        // Billing
        services.AddScoped<Contracts.Services.Billing.IBillingService, Services.Billing.BillingService>();

        // Spec 042 — SharedKernel write contracts (the write mirror of the ADR-006 read contracts)
        // so modules that cannot reference Finance (e.g. Aonik.Commerce) can bill and fund an order.
        services.AddScoped<SharedKernel.Abstractions.Billing.IInvoiceWriter, Services.Integration.InvoiceWriter>();
        services.AddScoped<SharedKernel.Abstractions.Payments.IPaymentInitiator, Services.Integration.PaymentInitiator>();

        // Insights
        services.AddScoped<Contracts.Services.Insights.IMySpaceSummaryService, Services.Insights.MySpaceSummaryService>();

        // Orders — type-specific orchestration (bill payment, remittance). The generic,
        // type-agnostic Order spine (SharedKernel.Abstractions.Ordering.IOrderService) is
        // registered by Aonik.Ordering's OrderingModule (Spec 041 / ADR-011 Phase 3).
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
        services.AddScoped<SharedKernel.Abstractions.Ordering.ICustomerOrderHistoryReader, Services.Finance.Readers.CustomerOrderHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.ICustomerInvoiceHistoryReader, Services.Finance.Readers.CustomerInvoiceHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.ICustomerPaymentHistoryReader, Services.Finance.Readers.CustomerPaymentHistoryReader>();
        services.AddScoped<SharedKernel.Abstractions.Finance.IFxQuoteReader, Services.Finance.Readers.FxQuoteReader>();
        // Customer-facing order projection + cancel (the remittance-rich shape the Simi order
        // tools surface) and FX rate history — the read/command contracts that let the PF agent
        // surface relocate off Aonik.Finance.Contracts (Spec 027 S-Contracts / #118).
        services.AddScoped<SharedKernel.Abstractions.Ordering.ICustomerOrderService, Services.Finance.Readers.CustomerOrderService>();
        services.AddScoped<SharedKernel.Abstractions.Finance.IFxRateHistoryReader, Services.Finance.Readers.FxRateHistoryReader>();

        // ICustomerDataExportProvider and ICustomerDataImportConsumer
        // relocated to PersonalFinanceModule (Spec 027 Phase 3).

        // Cross-module provisioning contributor
        services.AddScoped<SharedKernel.Abstractions.ITenantProvisioningContributor, Services.Provisioning.FinanceTenantProvisioningContributor>();

        // Spec 080 — Finance's slice of the unified Customers registry: which parties hold an
        // invoiceable customer account.
        services.AddScoped<SharedKernel.Abstractions.ICustomerRegistryContributor, Services.Customers.BillingCustomerRegistryContributor>();

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
        services.AddScoped<Services.Seeding.Phases.CrossBorderPricingSeedPhase>();
        services.AddScoped<Services.Seeding.Phases.OrderActivitySeedPhase>();
        // HouseholdsSeedPhase + PersonalFinanceActivitySeedPhase relocated to
        // PersonalFinanceModule with PersonalFinanceDemoSeedContributor (Spec 027 S5).

        // IPersonalFinancePartyResolver relocated to PersonalFinanceModule (Spec 027 Phase 3).
        services.AddScoped<SharedKernel.Abstractions.IDemoSeedContributor, Services.Seeding.FinanceDemoSeedContributor>();

        // Pricing
        services.AddScoped<Contracts.Services.Pricing.IPricingService, Services.Pricing.PricingService>();
        services.AddScoped<Contracts.Services.Pricing.IPricingPolicyService, Services.Pricing.PricingPolicyService>();
        services.AddScoped<Contracts.Services.Pricing.IFxRateService, Services.Pricing.FxRateService>();
        services.AddScoped<Contracts.Services.Pricing.IFxQuoteService, Services.Pricing.FxQuoteService>();
        services.AddSingleton<SharedKernel.Abstractions.ICurrencyMetadataProvider, Services.Pricing.CurrencyMetadataProvider>();

        // ── Remittance (Spec 036) ────────────────────────────────────
        // Payabo B2C send-money orchestration: quote → confirm (lock → debit → connector →
        // transmission) → settle on partner webhook, over the shipped order / pricing / ledger /
        // payout / transmission / webhook primitives.
        services.AddScoped<Contracts.Services.Remittance.IRemittanceOrderService, Services.Remittance.RemittanceOrderService>();

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
        services.AddScoped<Contracts.Services.Partners.Connectors.IPartnerConnectorResolver,
            Services.Partners.Connectors.PartnerConnectorResolver>();

        // ── Partner-owned connector credentials (ADR-010 / Spec 042) ─
        // Bundles encrypt their own secret bytes via IDataProtection (a distinct purpose string), because
        // the settings store only encrypts statically-defined keys. The bundle service is the only path that
        // decrypts; the factory binds a runtime connector to a persisted Connector row + its resolved bundle.
        services.AddSingleton<Services.Partners.Connectors.Credentials.IConnectorCredentialProtector,
            Services.Partners.Connectors.Credentials.ConnectorCredentialProtector>();
        services.AddScoped<Services.Partners.Connectors.Credentials.ICredentialBundleService,
            Services.Partners.Connectors.Credentials.CredentialBundleService>();
        services.AddScoped<Services.Partners.Connectors.IPartnerConnectorFactory,
            Services.Partners.Connectors.PartnerConnectorFactory>();
        services.AddScoped<Contracts.Services.Partners.ICredentialBundleAdminService,
            Services.Partners.CredentialBundleAdminService>();

        // ── Flutterwave connector (Spec 037, Issue #129) ────────────
        // Registered ALONGSIDE the simulated connector (distinct ProviderCode), only when configured.
        // The remittance service selects by ProviderCode and falls back to "Simulated", so the
        // simulated registrations above MUST stay. Selection is code-based, so coexistence is safe.
        services.Configure<Services.Partners.Connectors.Flutterwave.FlutterwaveOptions>(
            configuration.GetSection("Finance:Partners:Flutterwave"));

        services.AddScoped<Services.Partners.Connectors.Flutterwave.IFlutterwaveConfigProvider,
            Services.Partners.Connectors.Flutterwave.FlutterwaveConfigProvider>();
        services.AddScoped<Services.Partners.Connectors.Flutterwave.FlutterwaveTokenProvider>();
        services.AddHttpClient(Services.Partners.Connectors.Flutterwave.FlutterwaveTokenProvider.IdpClientName);

        services.AddTransient<Services.Partners.Connectors.Flutterwave.FlutterwaveAuthHandler>();
        services.AddHttpClient<Services.Partners.Connectors.Flutterwave.FlutterwaveClient>()
            .AddHttpMessageHandler<Services.Partners.Connectors.Flutterwave.FlutterwaveAuthHandler>();

        services.AddTransient<Contracts.Services.Partners.Connectors.IPartnerPayoutConnector,
            Services.Partners.Connectors.Flutterwave.FlutterwavePayoutConnector>();
        services.AddSingleton<Contracts.Services.Partners.Connectors.IPartnerWebhookTranslator,
            Services.Partners.Connectors.Flutterwave.FlutterwaveWebhookTranslator>();

        // ── Flutterwave bills connector (Spec 040) ──────────────────
        // The v3 Bills API is a DIFFERENT transport from the v4 payout connector above: a static
        // secret key (FLWSECK-…) and base URL https://api.flutterwave.com/v3 — it cannot reuse the v4
        // client/token provider. Registered ALONGSIDE the simulated bill connector (distinct
        // ProviderCode), only usable once a secret key is supplied; until then it fails closed.
        services.Configure<Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillsOptions>(
            configuration.GetSection("Finance:Partners:Flutterwave:Bills"));
        services.AddScoped<Services.Partners.Connectors.Flutterwave.Bills.IFlutterwaveBillsConfigProvider,
            Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillsConfigProvider>();
        services.AddTransient<Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillsAuthHandler>();
        services.AddHttpClient<Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillsClient>()
            .AddHttpMessageHandler<Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillsAuthHandler>();
        services.AddTransient<Contracts.Services.Partners.Connectors.IPartnerBillPaymentConnector,
            Services.Partners.Connectors.Flutterwave.Bills.FlutterwaveBillPaymentConnector>();

        // Catalog
        services.AddScoped<Contracts.Services.Catalog.ICatalogService, Services.Catalog.CatalogService>();
        services.AddScoped<Contracts.Services.Catalog.IPublicCatalogService, Services.Catalog.PublicCatalogService>();
        services.AddScoped<Contracts.Services.Catalog.IBillerImportService, Services.Catalog.BillerImportService>();

        // PersonalFinance
        // IBillService, IDashboardService, IHouseholdService, IPersonalAccountService,
        // IPersonalAccountLinkService, IPersonalTransactionService, IStatementImportService,
        // ITransactionClassificationService, IPersonalFinanceInsightsService,
        // FinancialConnectionTransactionSyncOrchestrator, ICustomerInsightSnapshotGenerator,
        // ICustomerInsightSnapshotService, ICustomerInsightSnapshotReader, and
        // ICustomerInsightSnapshotForAi all relocated to PersonalFinanceModule
        // (Spec 027 Phase 3 + Phase 7 deferred-refactor wrap-up).
        // PlaidAccountLinkOptions config, the PlaidAccountLinkProviderGateway
        // HttpClient, and the IPersonalAccountLinkProviderGateway factory
        // relocated to PersonalFinanceModule alongside the account-link slice
        // (Spec 027 S-Acct, #126).
        // FinancialConnectionSyncOptions config, ITransactionAiClassifier, and
        // IPersonalFinanceNarrativeInsightsService relocated to
        // PersonalFinanceModule (Spec 027 S5, #118/#126) — the last PF service
        // registrations to move off Finance ahead of dropping the reference.
        // The entire FinancialLifeGraph cluster (Schema, Loader, SnapshotMetrics,
        // HydrationService, Service, SchemaService, TraversalService,
        // CacheInvalidator, ValidationService, WriteService, InferenceService,
        // RetrievalService) has been relocated to PersonalFinanceModule
        // (Spec 027 Phase 3 + Phase 7 deferred-refactor wrap-up).
        // IFinancialContextService likewise relocated to PersonalFinanceModule.

        // Cross-module IPersonalProfileProvisioner and IUserBriefDataProvider
        // relocated to PersonalFinanceModule (Spec 027 Phase 3).

        // The IPersonalAccountLinkProviderGateway factory and the entire
        // account-link slice (AccountConnectionSyncOptions, IAccountTransactionCategorizer,
        // AccountTransactionSyncOrchestrator, IAccountLinkService) relocated to
        // PersonalFinanceModule (Spec 027 S-Acct, #126).

        // ── Finance AI Insights ──────────────────────────────────────
        services.AddScoped<Services.Ai.InvoiceInsightWorkflow>();
        services.AddScoped<Contracts.Services.Ai.IFinanceInsightsService, Services.Ai.FinanceInsightsService>();

        // ── Finance Domain Agent ─────────────────────────────────────
        // Registered as IDomainAgentDescriptor for the orchestrator to discover.
        services.AddSingleton<IDomainAgentDescriptor, FinanceAgentDescriptor>();

        // Spec 032 (finding C3) — Finance's tool-approval classification. The central
        // IToolApprovalGate (registered in AgentsModule) discovers every module manifest and
        // uses this one to wrap the finance agent's mutating tools before they reach the model.
        services.AddSingleton<IToolApprovalManifest, FinanceToolApprovalManifest>();

        // Spec 027 S1c2 (#118): the PersonalFinance ("Simi") agent surface — the
        // personal-finance / financial-life-graph / Compass / Spec-025 sub-agent
        // descriptors and the PersonalFinanceToolApprovalManifest — relocated to
        // PersonalFinanceModule alongside the Agents/ tool tree.

        // Spec 032 §7.4 — durable-execution handlers for the High-tier money tools. The approval
        // gate marshals finance_capture_payment / finance_cancel_payment / finance_create_payment_intent
        // / finance_mark_invoice_paid into a Proposal; these keyed handlers are the ONLY path that
        // reaches the Finance service, and only after the Spec 030 dispatcher runs them on approval.
        services.AddKeyedScoped<IProposalHandler, Agents.Proposals.CapturePaymentProposalHandler>(
            Agents.Proposals.CapturePaymentProposalHandler.ProposalTypeKey);
        services.AddKeyedScoped<IProposalHandler, Agents.Proposals.CancelPaymentProposalHandler>(
            Agents.Proposals.CancelPaymentProposalHandler.ProposalTypeKey);
        services.AddKeyedScoped<IProposalHandler, Agents.Proposals.CreatePaymentIntentProposalHandler>(
            Agents.Proposals.CreatePaymentIntentProposalHandler.ProposalTypeKey);
        services.AddKeyedScoped<IProposalHandler, Agents.Proposals.MarkInvoicePaidProposalHandler>(
            Agents.Proposals.MarkInvoicePaidProposalHandler.ProposalTypeKey);

        // ── Global Seed Contributors ────────────────────────────────────
        services.AddScoped<Services.Seeding.FinancePricingSeedContributor>();
        services.AddScoped<IGlobalSeedContributor>(sp =>
            sp.GetRequiredService<Services.Seeding.FinancePricingSeedContributor>());
        // PersonalFinanceSeedContributor (PersonalProfile backfill) relocated to
        // PersonalFinanceModule (Spec 027 S5, #118/#126).

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
