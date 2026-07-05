using Aonik.PersonalFinance.Agents;
using Aonik.PersonalFinance.Agents.CodeAct;
using Aonik.PersonalFinance.Contracts.Services.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Services.Accounts;
using Aonik.PersonalFinance.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance;

/// <summary>
/// Module registration for the PersonalFinance bounded context.
/// Owns: Households, PersonalAccounts, PersonalTransactions, Bills,
/// Subscriptions, DebtRepayments, Budgets, Goals, FinancialContext,
/// FinancialLifeGraph, CustomerInsight, StatementImports, and the
/// FinancialConnection (Plaid) integration.
///
/// PersonalFinance is the entire substrate of the B2C Payabo product.
/// Extracted from <c>Aonik.Finance</c> per Spec 027 so the Payabo product
/// can evolve independently of Aonik's core money plumbing (Ledger,
/// Orders, Payments, Billing, Pricing, Partners, Catalog).
///
/// Phase 1 of the spec: this file is intentionally a skeleton. Subsequent
/// phases migrate entities, services, endpoints, agents, and seed phases
/// into this module without changing the public <c>/personal-finance/*</c>
/// HTTP contract or the SQL physical layout.
/// </summary>
public sealed class PersonalFinanceModule : IModule
{
    public static string Name => "PersonalFinance";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register PersonalFinanceDbContext — module-scoped DbContext over the
        // same physical SQL database as AonikDbContext / FinanceDbContext.
        services.AddDbContext<PersonalFinanceDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"PersonalFinanceDb_{Guid.NewGuid()}";
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

        // ── PersonalFinance services (relocated from FinanceModule) ──
        // Spec 027 Phase 3: services migrate here progressively. The duplicate
        // registration is removed from FinanceModule when each service moves.
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<ICareEntityService, CareEntityService>();
        services.AddScoped<ICareEntityProfileService, CareEntityProfileService>();
        services.AddScoped<ICareEntityPhotoService, CareEntityPhotoService>();
        services.AddScoped<IPaymentLogService, PaymentLogService>();
        services.AddScoped<IPaymentLogSummaryService, PaymentLogSummaryService>();
        services.AddScoped<CircleService>();
        services.AddScoped<ICircleService>(sp => sp.GetRequiredService<CircleService>());
        services.AddScoped<ICircleVisibility>(sp => sp.GetRequiredService<CircleService>());
        services.AddScoped<ISupportStatementService, SupportStatementService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ICommitmentService, CommitmentService>();
        services.AddScoped<ITransactionAttachmentService, TransactionAttachmentService>();
        services.AddScoped<IPersonalAccountService, PersonalAccountService>();
        services.AddScoped<IPersonalTransactionService, PersonalTransactionService>();
        services.AddScoped<IStatementImportService, StatementImportService>();
        services.AddScoped<ITransactionClassificationService, TransactionClassificationService>();
        services.AddScoped<IPersonalAccountLinkService, PersonalAccountLinkService>();
        services.AddScoped<IPersonalFinanceInsightsService, PersonalFinanceInsightsService>();
        services.AddScoped<FinancialConnectionTransactionSyncOrchestrator>();

        // Spec 027 S3 (#126): the PersonalFinance-side demo-data teardown port.
        // Platform's ReverseSeedPhase invokes this instead of touching the PF
        // DbSets directly (they now live solely on PersonalFinanceDbContext),
        // which keeps Platform free of a Platform -> PersonalFinance reference.
        services.AddScoped<Aonik.SharedKernel.Abstractions.PersonalFinance.IPersonalFinanceDemoDataReverser,
            Services.Seeding.PersonalFinanceDemoDataReverser>();

        // ── Plaid account-link provider gateway (Spec 027 S-Acct, #126) ─
        // Relocated from FinanceModule. Backs both the account-link slice below
        // and the FinancialConnection sync orchestrator / PersonalAccountLinkService
        // above. The factory returns the live Plaid gateway when configured,
        // otherwise the simulated one.
        services.Configure<PlaidAccountLinkOptions>(
            configuration.GetSection("Finance:PersonalFinance:Plaid"));
        services.AddHttpClient<PlaidAccountLinkProviderGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PlaidAccountLinkOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });
        services.AddTransient<IPersonalAccountLinkProviderGateway>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PlaidAccountLinkOptions>>().Value;
            if (options.IsConfigured())
            {
                return sp.GetRequiredService<PlaidAccountLinkProviderGateway>();
            }

            return new PlaidSimulatedAccountLinkProviderGateway();
        });

        // ── Accounts (Tenant-Scoped Bank Linking) slice (Spec 027 S-Acct, #126) ─
        // Relocated from FinanceModule alongside the Services/Accounts subtree.
        services.Configure<AccountConnectionSyncOptions>(
            configuration.GetSection("Finance:Accounts:LinkedAccountSync"));
        services.AddScoped<IAccountTransactionCategorizer, AccountTransactionCategorizer>();
        services.AddScoped<AccountTransactionSyncOrchestrator>();
        services.AddScoped<IAccountLinkService, AccountLinkService>();

        // ── FinancialLifeGraph cluster (Spec 027 Phase 3 continued) ─
        services.AddSingleton<FinancialLifeGraphSchema>();
        services.AddScoped<FinancialLifeGraphLoader>();
        services.AddScoped<FinancialLifeGraphSnapshotMetrics>();
        services.AddScoped<FinancialLifeGraphHydrationService>();
        services.AddScoped<FinancialLifeGraphService>();
        services.AddScoped<IFinancialLifeGraphService>(sp => sp.GetRequiredService<FinancialLifeGraphService>());
        services.AddScoped<IFinancialLifeGraphCacheInvalidator, FinancialLifeGraphCacheInvalidator>();
        services.AddScoped<IFinancialLifeGraphSchemaService, FinancialLifeGraphSchemaService>();
        services.AddScoped<IFinancialLifeGraphTraversalService, FinancialLifeGraphTraversalService>();
        // Validation + Write services depend on SharedKernel readers
        // (IPartyReader / ICustomerOrderHistoryReader / ICustomerInvoiceHistoryReader /
        // ICustomerPaymentHistoryReader) registered by Finance + Platform.
        services.AddScoped<FinancialLifeGraphValidationService>();
        services.AddScoped<FinancialLifeGraphWriteService>();
        services.AddScoped<FinancialLifeGraphInferenceService>();
        services.AddScoped<IFinancialLifeGraphRetrievalService, FinancialLifeGraphRetrievalService>();

        // Spec 030 — generic proposal-dispatcher handlers for FLG annotations.
        // Registered keyed by the ProposalType string so IProposalDispatcher /
        // IProposalRejectionDispatcher in Aonik.Agents can resolve them when
        // /ai/proposals/{id}/approve|dismiss fires on a FinancialLifeGraphAnnotation
        // proposal. No PF endpoint executes these directly any more.
        services.AddKeyedScoped<IProposalHandler, FinancialLifeGraphAnnotationProposalHandler>(
            FinancialLifeGraphAnnotationProposalHandler.ProposalTypeKey);
        services.AddKeyedScoped<IProposalRejectionHandler, FinancialLifeGraphAnnotationProposalRejectionHandler>(
            FinancialLifeGraphAnnotationProposalHandler.ProposalTypeKey);

        // ── Household + CustomerInsight + Dashboard + FinancialContext clusters
        //    (Spec 027 Phase 7 deferred-refactor wrap-up) ────────────
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IFinancialContextService, FinancialContextService>();
        services.AddScoped<ICustomerInsightSnapshotGenerator, CustomerInsightSnapshotGenerator>();
        services.AddScoped<ICustomerInsightSnapshotService, CustomerInsightSnapshotService>();
        services.AddScoped<ICustomerInsightSnapshotReader, CustomerInsightSnapshotReader>();
        // SharedKernel-shaped wrapper consumed by Aonik.Ai's CustomerInsightAiSummaryService
        // — keeps Ai free of a back-pointing reference on PersonalFinance.
        services.AddScoped<SharedKernel.Abstractions.PersonalFinance.ICustomerInsightSnapshotForAi, CustomerInsightSnapshotForAiAdapter>();

        // ── AONIK Compass (Spec 021) — goal programmes, plan lifecycle,
        //    deterministic safe-to-spend guidance, and Compass proposals.
        //    The CompassPlannerAgentDescriptor sub-agent itself is registered
        //    below alongside the other PersonalFinance IDomainAgentDescriptors.
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<ICompassPlanService, CompassPlanService>();
        services.AddScoped<ICompassGuidanceService, CompassGuidanceService>();
        services.AddScoped<ICompassPlanGenerator, CompassPlanGenerator>();

        // ── Cross-module adapter implementations (relocated) ────────
        services.AddScoped<IPersonalProfileProvisioner, PersonalProfileProvisioner>();
        services.AddScoped<IUserBriefDataProvider, UserBriefDataProvider>();
        services.AddScoped<IPersonalFinancePartyResolver, PersonalFinancePartyResolver>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.ICustomerDataExportProvider, CustomerDataExportProvider>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.ICustomerDataImportConsumer, CustomerDataImportConsumer>();

        // Spec 028 §15 — Chronicle taxonomy mapper, consumed by Finance's
        // AccountTransactionCategorizer through SharedKernel so neither module
        // needs a project reference on the other once Spec 027 Phase 3 lands.
        // Singleton: the implementation is a pure function over static data.
        services.AddSingleton<Aonik.SharedKernel.Abstractions.Finance.Categorization.IChronicleCategoryMapper,
            Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper>();

        // ── CodeAct Sandbox Providers (Spec 027 Phase 5) ────────────
        // Backs the wrapping `execute_code` AIFunction the three PF sub-agents
        // (pf-insights, pf-forecast, pf-classify) surface to the LLM. The provider
        // selector reads Ai:CodeAct:Provider and returns Hyperlight (local Linux
        // dev), AcaSessions (cloud), or Null (forces tool-loop fallback).
        services.AddOptions<AcaSessionsOptions>().BindConfiguration(AcaSessionsOptions.SectionName);
        services.AddSingleton<CodeActCallbackNonceService>(sp =>
            new CodeActCallbackNonceService(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CodeActCallbackNonceService>>()));
        services.AddHttpClient<AcaSessionsClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AcaSessionsOptions>>().Value;
            var endpoint = opts.PoolManagementEndpoint?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint + "/", UriKind.Absolute, out var baseUri))
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

        // ── PersonalFinance Domain Agents (Spec 027 S1c2, #118) ──────
        // The "Simi" agent surface, relocated from FinanceModule with the Agents/
        // tool tree. Registered as IDomainAgentDescriptor for the orchestrator to
        // discover; the tool-approval manifest plugs into the same central
        // IToolApprovalGate (AgentsModule) as every other module's.
        services.AddSingleton<IDomainAgentDescriptor, PersonalFinanceAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, FinancialLifeGraphAgentDescriptor>();
        // Spec 025 — three analytical sub-agents Simi invokes via
        // pf_run_insights / pf_run_forecast / pf_run_classify_review.
        services.AddSingleton<IDomainAgentDescriptor, PfInsightsAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfForecastAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfClassifyAgentDescriptor>();
        // Spec 021 — AONIK Compass planning specialist (pf_run_compass_planner). Never user-facing.
        services.AddSingleton<IDomainAgentDescriptor, CompassPlannerAgentDescriptor>();
        // Spec 032 — Simi's tool-approval classification (all Medium/Low; PersonalFinance moves no money).
        services.AddSingleton<IToolApprovalManifest, PersonalFinanceToolApprovalManifest>();

        return services;
    }
}

/// <summary>
/// Extension methods for registering the PersonalFinance module in the DI container.
/// </summary>
public static class PersonalFinanceModuleExtensions
{
    /// <summary>
    /// Adds the PersonalFinance module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddPersonalFinanceModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => PersonalFinanceModule.ConfigureServices(services, configuration);
}
