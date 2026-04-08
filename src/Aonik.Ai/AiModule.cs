using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Middleware;
using Aonik.Ai.Persistence;
using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Aonik.Ai;

/// <summary>
/// AI module registration. Owns AI Platform entities (providers, models, policies,
/// prompts, tools, runs, traces, feedback, evals, insights, signals).
/// Provides horizontal AI infrastructure consumed by domain modules.
/// </summary>
public sealed class AiModule : IModule
{
    public static string Name => "Ai";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register AiDbContext
        // Shares the same physical database as AonikDbContext, PlatformDbContext, and FinanceDbContext
        // using dbo schema + module table prefixes.
        services.AddDbContext<AiDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"AiDb_{Guid.NewGuid()}";
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

        // ── AI Infrastructure ────────────────────────────────────────
        services.Configure<Aonik.Ai.Services.TextToSpeechOptions>(configuration.GetSection("AI:TextToSpeech"));

        // File-based prompt store (loads .md templates from disk) — used as fallback
        services.AddSingleton<FileBasedPromptStore>(sp =>
        {
            var promptPath = configuration["AI:PromptTemplatesPath"];
            return new FileBasedPromptStore(promptPath);
        });

        // Tenant-aware prompt store: DB overrides (tenant → global) → file-based fallback
        services.AddScoped<IPromptStore, TenantAwarePromptStore>();

        // IChatClient — registered directly with AuditMiddleware in the pipeline.
        // Provider is selected via AI:Provider config key (Stub | OpenAI | AzureOpenAI).
        services.AddScoped<IChatClient>(sp =>
        {
            var provider = configuration["AI:Provider"] ?? "Stub";
            var logger = sp.GetRequiredService<ILogger<AiModule>>();
            logger.LogInformation("Creating IChatClient for provider: {Provider}", provider);

            IChatClient innerClient = provider.ToLowerInvariant() switch
            {
                "stub" => new StubChatClient(),

                "openai" => new OpenAI.Chat.ChatClient(
                    configuration["AI:OpenAI:Model"] ?? "gpt-5-mini",
                    configuration["AI:OpenAI:ApiKey"] ?? throw new InvalidOperationException(
                        "AI:OpenAI:ApiKey configuration is required when using the OpenAI provider. " +
                        "Set it via appsettings, environment variable, or user-secrets."))
                    .AsIChatClient(),

                "azureopenai" or "azure_openai" or "azure-openai" => throw new NotSupportedException(
                    "Azure OpenAI provider is not yet implemented. " +
                    "Add the Azure.AI.OpenAI package and configure AI:AzureOpenAI:Endpoint, AI:AzureOpenAI:ApiKey, and AI:AzureOpenAI:DeploymentName."),

                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{provider}'. Supported values: Stub, OpenAI, AzureOpenAI.")
            };

            // Determine whether to include sensitive data (prompts, responses, tool args)
            // in OpenTelemetry traces. Only enable in development/testing environments.
            var enableSensitiveData = configuration.GetValue<bool>("AI:OpenTelemetry:EnableSensitiveData");

            // Build the middleware pipeline: innerClient -> OpenTelemetry -> AuditMiddleware
            // OpenTelemetry is outermost to capture the full request lifecycle including
            // chat spans and tool execution spans per GenAI semantic conventions.
            return innerClient
                .AsBuilder()
                .UseOpenTelemetry(
                    sourceName: AiTelemetry.SourceName,
                    configure: cfg => cfg.EnableSensitiveData = enableSensitiveData)
                .Use((inner, _) => new AuditMiddleware(
                    inner,
                    sp.GetRequiredService<IAiRunWriter>(),
                    sp.GetRequiredService<ILogger<AuditMiddleware>>()))
                .Build();
        });

        // ── Image Generation ─────────────────────────────────────────
        var aiProvider = (configuration["AI:Provider"] ?? "Stub").ToLowerInvariant();
        if (aiProvider == "openai")
            services.AddScoped<IContentImageGenerator, ContentImageGenerator>();
        else
            services.AddSingleton<IContentImageGenerator, StubContentImageGenerator>();

        // ── AI Services ──────────────────────────────────────────────
        // Provider & Model catalog CRUD + model resolution.
        // AiModelService implements both IAiModelService (module-internal CRUD)
        // and IAiModelResolver (cross-module model resolution via SharedKernel).
        services.AddScoped<AiModelService>();
        services.AddScoped<IAiModelService>(sp => sp.GetRequiredService<AiModelService>());
        services.AddScoped<IAiModelResolver>(sp => sp.GetRequiredService<AiModelService>());
        services.AddScoped<IAiModelCatalogImportService, AiModelCatalogImportService>();

        // AI task profile resolution — composes model + prompt resolution
        services.AddScoped<IAiTaskProfileResolver, AiTaskProfileResolver>();

        // Cross-module AI task reader (used by Agents playground endpoint)
        services.AddScoped<IAiTaskReader, AiTaskReader>();

        // Prompt spec CRUD — manages versioned prompt templates
        services.AddScoped<Contracts.Services.IPromptSpecService, PromptSpecService>();

        // Route policy CRUD — manages AI model routing policies
        services.AddScoped<Contracts.Services.IRoutePolicyService, RoutePolicyService>();

        // AI task CRUD — manages AI task definitions with prompt templates and metadata
        services.AddScoped<Contracts.Services.IAiTaskService, AiTaskService>();

        // Insight persistence — consumed by domain modules via IInsightWriter contract
        services.AddScoped<IInsightWriter, InsightWriter>();
        services.AddScoped<IInsightReader, InsightReader>();
        services.AddScoped<IAiRunWriter, AiRunWriter>();
        services.AddScoped<ICustomerInsightAiSummaryService, CustomerInsightAiSummaryService>();
        services.AddScoped<ICustomerInsightAiSummaryReader, CustomerInsightAiSummaryReader>();
        services.AddSingleton<ITextToSpeechRateLimiter, TextToSpeechRateLimiter>();
        services.AddScoped<ITextToSpeechService, TextToSpeechService>();
        RegisterTtsProvider<ElevenLabsTextToSpeechProvider>(services, configuration, opts => opts.ElevenLabsBaseUrl);
        RegisterTtsProvider<MistralTextToSpeechProvider>(services, configuration, opts => opts.MistralBaseUrl);

        // User memory — manages AI-learned facts, preferences, and corrections about users
        services.AddScoped<Contracts.Services.IUserMemoryService, UserMemoryService>();

        // Cross-module data provider for the UserBriefProjector (Agents module)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserBriefAiDataProvider, UserBriefAiDataProvider>();

        // Cross-module provisioning contributor
        services.AddScoped<Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor, Services.AiTenantProvisioningContributor>();

        // Global seed contributors (on-demand via admin endpoint)
        services.AddScoped<Aonik.SharedKernel.Abstractions.IGlobalSeedContributor, Services.Seeding.PromptSpecSeedContributor>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.IGlobalSeedContributor, Services.Seeding.AiTaskSeedContributor>();

        return services;
    }

    private static void RegisterTtsProvider<TProvider>(
        IServiceCollection services,
        IConfiguration configuration,
        Func<Aonik.Ai.Services.TextToSpeechOptions, string> baseUrlSelector)
        where TProvider : class, ITextToSpeechProvider
    {
        services.AddHttpClient<TProvider>((_, client) =>
        {
            var options = configuration.GetSection("AI:TextToSpeech").Get<Aonik.Ai.Services.TextToSpeechOptions>()
                ?? new Aonik.Ai.Services.TextToSpeechOptions();
            client.BaseAddress = new Uri(baseUrlSelector(options));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddScoped<ITextToSpeechProvider>(sp => sp.GetRequiredService<TProvider>());
    }
}

/// <summary>
/// Extension methods for registering the AI module in the DI container.
/// </summary>
public static class AiModuleExtensions
{
    /// <summary>
    /// Adds the AI module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => AiModule.ConfigureServices(services, configuration);
}
