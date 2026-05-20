using Aonik.Finance.Agents.CodeAct;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
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
