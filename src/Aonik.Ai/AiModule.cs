using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Middleware;
using Aonik.Ai.Observability;
using Aonik.Ai.Persistence;
using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
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
        services.Configure<AiTraceExplorerOptions>(configuration.GetSection("AI:TraceExplorer"));

        // Public ISpeechTextNormalizer facade — exposes the internal SpeechTextNormalizer
        // to non-Ai modules (notably Aonik.Voice) without leaking implementation
        // details. Singleton because the underlying static method is stateless.
        // See docs/specifications/022.aonik-voice-realtime.md Phase 2.
        services.AddSingleton<Aonik.SharedKernel.Abstractions.Ai.ISpeechTextNormalizer, Services.SpeechTextNormalizerFacade>();

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
                    new System.ClientModel.ApiKeyCredential(
                        aiSettings.OpenAiApiKey ?? throw new InvalidOperationException(
                            "OpenAI API key is required when using the OpenAI provider. " +
                            "Configure it via the Settings module (Ai.OpenAI.ApiKey) or appsettings (AI:OpenAI:ApiKey).")),
                    BuildOpenAiClientOptions(configuration))
                    .AsIChatClient(),

                "azureopenai" or "azure_openai" or "azure-openai" => throw new NotSupportedException(
                    "Azure OpenAI provider is not yet implemented. " +
                    "Add the Azure.AI.OpenAI package and configure the Azure OpenAI settings."),

                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{aiSettings.Provider}'. Supported values: Stub, OpenAI, AzureOpenAI.")
            };

            // Determine whether to include sensitive data (prompts, responses, tool args)
            // in OpenTelemetry traces. Only enable in development/testing environments.
            var settingProvider = sp.GetRequiredService<ISettingProvider>();
            var enableSensitiveDataRaw = settingProvider.GetAsync(AiSettingNames.OpenTelemetryEnableSensitiveData)
                .GetAwaiter().GetResult();
            var enableSensitiveData = bool.TryParse(
                enableSensitiveDataRaw ?? configuration["AI:OpenTelemetry:EnableSensitiveData"],
                out var parsedEnableSensitiveData)
                && parsedEnableSensitiveData;

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
                    sp.GetService<ICurrentUserProvider>(),
                    enableSensitiveData))
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
                    new System.ClientModel.ApiKeyCredential(
                        aiSettings.OpenAiApiKey ?? throw new InvalidOperationException(
                            "OpenAI API key is required when using the OpenAI provider.")),
                    BuildOpenAiClientOptions(configuration))
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

        // Route policy CRUD — manages AI model routing policies
        services.AddScoped<Contracts.Services.IRoutePolicyService, RoutePolicyService>();

        // AI task CRUD — manages AI task definitions with prompt templates and metadata
        services.AddScoped<Contracts.Services.IAiTaskService, AiTaskService>();

        // Insight persistence — consumed by domain modules via IInsightWriter contract
        services.AddScoped<IInsightWriter, InsightWriter>();
        // Domain-event counters (runs.completed, runs.tokens_used) on the
        // existing "Aonik.Ai" meter; subscribed in ServiceDefaults.
        services.AddSingleton<Aonik.Ai.Observability.AiRunMetrics>();
        services.AddScoped<IAiRunWriter, AiRunWriter>();
        services.AddScoped<AiTraceQueryService>();
        services.AddScoped<AiTraceExplorerService>();
        services.AddScoped<IAiTraceReader, AppInsightsAiTraceReader>();
        services.AddScoped<IAiTraceReader, LangfuseAiTraceReader>();
        services.AddScoped<ICustomerInsightAiSummaryService, CustomerInsightAiSummaryService>();
        services.AddScoped<ICustomerInsightAiSummaryReader, CustomerInsightAiSummaryReader>();

        // Capture-parse (Spec 047) — image/text/transcript → structured draft proposal.
        services.AddScoped<Contracts.Services.ICaptureParseService, Services.Capture.CaptureParseService>();
        services.AddSingleton<ITextToSpeechRateLimiter, TextToSpeechRateLimiter>();
        services.AddScoped<TextToSpeechService>();
        services.AddScoped<ITextToSpeechService>(sp => sp.GetRequiredService<TextToSpeechService>());
        services.AddScoped<IStreamingTextToSpeechService>(sp => sp.GetRequiredService<TextToSpeechService>());
        services.AddSingleton<ITtsCache, TtsCache>();
        services.AddScoped<ISpeechRenderer, SpeechRenderer>();
        RegisterTtsProvider<ElevenLabsTextToSpeechProvider>(services, configuration, opts => opts.ElevenLabsBaseUrl);
        RegisterTtsProvider<MistralTextToSpeechProvider>(services, configuration, opts => opts.MistralBaseUrl);

        // User memory — manages AI-learned facts, preferences, and corrections about users.
        // The SQL-based implementation is always registered as a concrete type.
        // The IUserMemoryService factory (which selects between SQL and Qdrant backends
        // based on the Ai.UserMemory.Backend setting) is registered in Infrastructure's
        // DependencyInjection.cs since it needs access to QdrantUserMemoryService.
        services.AddScoped<UserMemoryService>();

        // Decision-aware learning (Spec 041): tenant pattern store + user rationale recall + the
        // outcome-extraction service the resolution events feed.
        services.AddScoped<Contracts.Services.IDecisionPatternService, DecisionPatternService>();
        services.AddScoped<Contracts.Services.IDecisionRationaleService, DecisionRationaleService>();
        services.AddScoped<Contracts.Services.IDecisionOutcomeExtractor, DecisionOutcomeExtractionService>();
        // The outbox handler that drives extraction whenever a DecisionResolvedEvent is dispatched.
        // The InProcessEventBus resolves handlers by IEventHandler<TEvent> from DI, so an explicit
        // registration is enough — no per-assembly AddEventBus scan is needed in the Ai module.
        services.AddScoped<
            Aonik.SharedKernel.Events.IEventHandler<Aonik.SharedKernel.Events.Integration.DecisionResolvedEvent>,
            IntegrationEvents.DecisionResolvedEventHandler>();

        // Cross-module data provider for the UserBriefProjector (Agents module)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserBriefAiDataProvider, UserBriefAiDataProvider>();

        // Cross-module read aggregate consumed by Finance dashboards (e.g. MySpace).
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IAiRunStatsService, Services.Insights.AiRunStatsService>();

        // Cross-module recall provider for agent tools (semantic user memory search)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserMemoryRecallProvider, UserMemoryRecallProvider>();

        // Cross-module save provider for agent tools and conversation summary extraction
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IUserMemorySaveProvider, UserMemorySaveProvider>();

        // Cross-module provisioning contributor
        services.AddScoped<Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor, Services.AiTenantProvisioningContributor>();

        // Global seed contributors (on-demand via admin endpoint)
        services.AddScoped<Aonik.SharedKernel.Abstractions.IGlobalSeedContributor, Services.Seeding.AiTaskSeedContributor>();

        // Pre-warm the chat client on startup to avoid the TLS handshake cost
        // on the first real request. Skipped when provider is "stub".
        services.AddHostedService<ChatClientWarmupService>();

        return services;
    }

    /// <summary>
    /// Builds the <see cref="OpenAI.OpenAIClientOptions"/> shared by the
    /// OpenAI ChatClient and EmbeddingClient. Sets an explicit
    /// <c>NetworkTimeout</c> so a stuck OpenAI call fails on a bounded
    /// schedule instead of hanging forever — the OpenAI SDK does not
    /// apply an HttpClient.Timeout by default. Configurable via
    /// <c>AI:OpenAI:NetworkTimeoutSeconds</c> (default 120 s, generous
    /// enough for a long completion but short enough that a hung
    /// upstream surfaces as a real error).
    /// </summary>
    private static OpenAI.OpenAIClientOptions BuildOpenAiClientOptions(IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("AI:OpenAI:NetworkTimeoutSeconds") ?? 120;
        return new OpenAI.OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
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
            // Explicit timeout. HttpClient defaults to 100s when unset, which
            // is far too lenient for an interactive voice surface — a hung
            // provider would block the AGUI stream for nearly two minutes.
            // Configurable via AI:TextToSpeech:TimeoutSeconds.
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddScoped<ITextToSpeechProvider>(sp => sp.GetRequiredService<TProvider>());

        // ── Spec 096 S1 — the content safety gate ────────────────────────────
        // The gate is the only issuer of ContentDeliveryPermit, and the sweeper ships WITH it: an
        // expiry column deletes nothing, and artefacts start accumulating the moment blocking works.
        services.Configure<Services.Safety.SafetyOptions>(
            configuration.GetSection(Services.Safety.SafetyOptions.SectionName));
        services.AddScoped<SharedKernel.Abstractions.Safety.ISafetyPolicyReader, Services.Safety.SafetyPolicyReader>();
        services.AddScoped<Services.Safety.ISafetyIncidentRecorder, Services.Safety.SafetyIncidentRecorder>();
        // Constructed via a factory because IUsageMeter is optional here: Aonik.Ai must not require
        // Subscriptions to be registered in order to keep children safe, and a host that omits it
        // simply has nothing to release.
        services.AddScoped<SharedKernel.Abstractions.Safety.IContentSafetyGate>(sp =>
            new Services.Safety.ContentSafetyGate(
                sp.GetRequiredService<Persistence.AiDbContext>(),
                sp.GetRequiredService<SharedKernel.Abstractions.Safety.ISafetyPolicyReader>(),
                sp.GetServices<SharedKernel.Abstractions.Safety.IContentClassifier>(),
                sp.GetRequiredService<Services.Safety.ISafetyIncidentRecorder>(),
                sp.GetRequiredService<Services.Safety.IGuardianPreReviewService>(),
                sp.GetService<SharedKernel.Abstractions.Subscriptions.IUsageMeter>(),
                sp.GetRequiredService<SharedKernel.Abstractions.Multitenancy.ITenantProvider>(),
                sp.GetRequiredService<SharedKernel.Abstractions.IClock>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.Safety.SafetyOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.Safety.ContentSafetyGate>>()));
        services.AddScoped<Services.Safety.IPreservedMaterialService, Services.Safety.PreservedMaterialService>();
        services.AddScoped<Services.Safety.ILegalHoldReader>(sp =>
            (Services.Safety.PreservedMaterialService)sp.GetRequiredService<Services.Safety.IPreservedMaterialService>());
        services.AddScoped<Services.Safety.ISafetyRetentionSweeper, Services.Safety.SafetyRetentionSweeper>();
        services.AddScoped<Services.Safety.ISafetyModelRouter, Services.Safety.SafetyModelRouter>();
        services.AddScoped<Services.Safety.ISafetyPolicyService, Services.Safety.SafetyPolicyService>();
        services.AddScoped<SharedKernel.Abstractions.Safety.IRequestConstraint, Services.Safety.BandRequestConstraint>();
        services.AddScoped<Services.Safety.IGuardianReviewService, Services.Safety.GuardianReviewService>();
        services.AddScoped<Services.Safety.IGuardianPreReviewService, Services.Safety.GuardianPreReviewService>();

        // One routed classifier per modality. Each resolves its model through AiRoutePolicy and
        // refuses a provider the subject's terms do not name — so a routing edit cannot redirect a
        // child's content to a company the family has never heard of.
        //
        // NOTE: no ISafetyClassificationProvider is registered by default, because no
        // classification vendor is configured in this solution. That is not an oversight: the gate
        // fails closed, so child-facing generation is refused until one is wired. Shipping a
        // permissive stub would be strictly worse than shipping nothing.
        foreach (var modality in new[] { SharedKernel.Abstractions.Safety.SafetyModalities.Text, SharedKernel.Abstractions.Safety.SafetyModalities.Image })
        {
            var captured = modality;
            services.AddScoped<SharedKernel.Abstractions.Safety.IContentClassifier>(sp =>
                new Services.Safety.RoutedContentClassifier(
                    captured,
                    sp.GetRequiredService<Services.Safety.ISafetyModelRouter>(),
                    sp.GetServices<Services.Safety.ISafetyClassificationProvider>(),
                    sp.GetRequiredService<Persistence.AiDbContext>(),
                    sp.GetRequiredService<SharedKernel.Abstractions.Multitenancy.ITenantProvider>(),
                    sp.GetRequiredService<SharedKernel.Abstractions.IClock>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.Safety.RoutedContentClassifier>>()));
        }

        // ── Spec 096 S5 — voice ──────────────────────────────────────────────
        // Narration is judged in two legs that both have to run: the transcript as text, and the
        // audio for what a transcript cannot carry — tone, pacing, distress, a gentle sentence read
        // in a terrifying voice. The composite refuses if either leg is missing, so voice is never
        // enabled as a side effect of another modality being configured.
        //
        // No ISpeechTranscriber is registered by default, for the same reason no classification
        // adapter is. Narration is therefore refused today, which is the correct state.
        services.AddScoped<SharedKernel.Abstractions.Safety.IContentClassifier>(sp =>
            new Services.Safety.SpeechContentClassifier(
                sp.GetService<Services.Safety.ISpeechTranscriber>(),
                RoutedClassifier(sp, SharedKernel.Abstractions.Safety.SafetyModalities.Text),
                RoutedClassifier(sp, SharedKernel.Abstractions.Safety.SafetyModalities.Speech),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.Safety.SpeechContentClassifier>>()));

        services.AddScoped<Services.Safety.IChildNarrationService, Services.Safety.ChildNarrationService>();
    }

    /// <summary>
    /// A routed classifier for one modality, built directly rather than resolved.
    ///
    /// <para>
    /// Resolving <c>IEnumerable&lt;IContentClassifier&gt;</c> from inside a factory that registers an
    /// <c>IContentClassifier</c> would recurse, so the speech composite constructs its two legs. It
    /// also keeps the legs honest: each resolves its own route and its own adapter, and a provider
    /// that does not name <c>speech</c> cannot satisfy the audio leg.
    /// </para>
    /// </summary>
    private static Services.Safety.RoutedContentClassifier RoutedClassifier(
        IServiceProvider sp, string modality)
        => new(
            modality,
            sp.GetRequiredService<Services.Safety.ISafetyModelRouter>(),
            sp.GetServices<Services.Safety.ISafetyClassificationProvider>(),
            sp.GetRequiredService<Persistence.AiDbContext>(),
            sp.GetRequiredService<SharedKernel.Abstractions.Multitenancy.ITenantProvider>(),
            sp.GetRequiredService<SharedKernel.Abstractions.IClock>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.Safety.RoutedContentClassifier>>());
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
