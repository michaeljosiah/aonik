using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Middleware;
using Aonik.Ai.Observability;
using Aonik.Ai.Persistence;
using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
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

        // NOTE: IAiProviderSettings is registered in Infrastructure's DependencyInjection.cs
        // because the implementation (AiProviderSettings) needs Aonik.Platform's ISettingProvider,
        // which Aonik.Ai does not reference.

        // IChatClient — registered with AuditMiddleware in the pipeline.
        // Provider is selected via IAiProviderSettings (Settings module → IConfiguration fallback).
        services.AddScoped<IChatClient>(sp =>
        {
            var aiSettings = sp.GetRequiredService<IAiProviderSettings>();
            var logger = sp.GetRequiredService<ILogger<AiModule>>();
            logger.LogInformation("Creating IChatClient for provider: {Provider}", aiSettings.Provider);

            IChatClient innerClient = aiSettings.Provider.ToLowerInvariant() switch
            {
                "stub" => new StubChatClient(),

                "openai" => new OpenAI.Chat.ChatClient(
                    aiSettings.OpenAiModel,
                    aiSettings.OpenAiApiKey ?? throw new InvalidOperationException(
                        "OpenAI API key is required when using the OpenAI provider. " +
                        "Configure it via the Settings module (Ai.OpenAI.ApiKey) or appsettings (AI:OpenAI:ApiKey)."))
                    .AsIChatClient(),

                "azureopenai" or "azure_openai" or "azure-openai" => throw new NotSupportedException(
                    "Azure OpenAI provider is not yet implemented. " +
                    "Add the Azure.AI.OpenAI package and configure the Azure OpenAI settings."),

                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{aiSettings.Provider}'. Supported values: Stub, OpenAI, AzureOpenAI.")
            };

            // Determine whether to include sensitive data (prompts, responses, tool args)
            // in OpenTelemetry traces. Only enable in development/testing environments.
            var enableSensitiveData = configuration.GetValue<bool>("AI:OpenTelemetry:EnableSensitiveData");

            // Build the middleware pipeline:
            //   innerClient -> OpenTelemetry -> AuditMiddleware -> TelemetryChatClient (outermost)
            //
            // TelemetryChatClient is intentionally last so it observes every
            // LLM call regardless of caller (chat endpoint, summariser,
            // projector, agent tool) and emits one structured `AiCallCompleted`
            // log + meter measurement per call. This is the source of truth
            // for the observability dashboard's AI tab.
            return innerClient
                .AsBuilder()
                .UseOpenTelemetry(
                    sourceName: AiTelemetry.SourceName,
                    configure: cfg => cfg.EnableSensitiveData = enableSensitiveData)
                .Use((inner, _) => new AuditMiddleware(
                    inner,
                    sp.GetRequiredService<IAiRunWriter>(),
                    sp.GetRequiredService<ILogger<AuditMiddleware>>()))
                .Use((inner, _) => new TelemetryChatClient(
                    inner,
                    sp.GetRequiredService<ILogger<TelemetryChatClient>>(),
                    sp.GetService<ITenantContext>(),
                    sp.GetService<ICurrentUserProvider>()))
                .Build();
        });

        // IEmbeddingGenerator — follows the same provider pattern as IChatClient.
        // Used by IEmbeddingService (in Infrastructure) for vector embedding generation.
        services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var aiSettings = sp.GetRequiredService<IAiProviderSettings>();
            var embeddingModel = configuration["Qdrant:EmbeddingModel"] ?? "text-embedding-3-small";
            var dimensions = configuration.GetValue<int?>("Qdrant:VectorDimensions") ?? 1536;

            IEmbeddingGenerator<string, Embedding<float>> generator = aiSettings.Provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAI.Embeddings.EmbeddingClient(
                    embeddingModel,
                    aiSettings.OpenAiApiKey ?? throw new InvalidOperationException(
                        "OpenAI API key is required when using the OpenAI provider."))
                    .AsIEmbeddingGenerator(defaultModelDimensions: dimensions),

                // Stub and other providers: no-op generator registered; IEmbeddingService
                // falls back to its built-in mock embedding generation.
                _ => new StubEmbeddingGenerator(embeddingModel, dimensions)
            };

            return generator;
        });

        // ── Image Generation ─────────────────────────────────────────
        // Runtime-switchable: both implementations registered, factory selects based on provider.
        services.AddScoped<ContentImageGenerator>();
        services.AddSingleton<StubContentImageGenerator>();
        services.AddScoped<IContentImageGenerator>(sp =>
        {
            var aiSettings = sp.GetRequiredService<IAiProviderSettings>();
            return aiSettings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<ContentImageGenerator>()
                : sp.GetRequiredService<StubContentImageGenerator>();
        });

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

        // User memory — manages AI-learned facts, preferences, and corrections about users.
        // The SQL-based implementation is always registered as a concrete type.
        // The IUserMemoryService factory (which selects between SQL and Qdrant backends
        // based on the Ai.UserMemory.Backend setting) is registered in Infrastructure's
        // DependencyInjection.cs since it needs access to QdrantUserMemoryService.
        services.AddScoped<UserMemoryService>();

        // Cross-module data provider for the UserBriefProjector (Agents module)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserBriefAiDataProvider, UserBriefAiDataProvider>();

        // Cross-module recall provider for agent tools (semantic user memory search)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserMemoryRecallProvider, UserMemoryRecallProvider>();

        // Cross-module save provider for agent tools and conversation summary extraction
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserMemorySaveProvider, UserMemorySaveProvider>();

        // Cross-module provisioning contributor
        services.AddScoped<Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor, Services.AiTenantProvisioningContributor>();

        // Global seed contributors (on-demand via admin endpoint)
        services.AddScoped<Aonik.SharedKernel.Abstractions.IGlobalSeedContributor, Services.Seeding.PromptSpecSeedContributor>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.IGlobalSeedContributor, Services.Seeding.AiTaskSeedContributor>();

        // Pre-warm the chat client on startup to avoid the TLS handshake cost
        // on the first real request. Skipped when provider is "stub".
        services.AddHostedService<ChatClientWarmupService>();

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
