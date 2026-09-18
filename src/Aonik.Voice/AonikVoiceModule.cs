using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
using Aonik.Voice.Configuration;
using Aonik.Voice.Library;
using Aonik.Voice.Persistence;
using Aonik.Voice.Pipeline;
using Aonik.Voice.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Voice;

/// <summary>
/// Voice module registration. Owns the <c>WSS /ai/voice</c> endpoint and the AONIK glue around
/// Voxa's <c>MicrosoftAgentVoice.CreateProcessor</c> + <c>WebSocketAudioSink</c> (see
/// <c>AonikVoiceAgent</c> + <c>ThreadReadyFrameSerializer</c>). Reuses Aonik.Agents services
/// (<c>IAgentContextualizer</c>, <c>IChatThreadManager</c>,
/// <c>IPostStreamPersistenceCoordinator</c>) and Aonik.Ai contracts
/// (<c>ISpeechTextNormalizer</c>) — does NOT duplicate them.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> for the full architecture,
/// especially Phase 0 (MAF isolation) and Phase 1.5 (read-only voice variant).
/// </para>
/// </summary>
public sealed class AonikVoiceModule : IModule
{
    public static string Name => "Voice";
    public static string Id => ModuleIds.Voice;

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Backend tool safety (FALLBACK) — classifies known pf_* tools as read-only or mutating by
        // naming prefix. Since Spec 032 voice's default is the FULL gated agent (mutating tools
        // enforced server-side by IToolApprovalGate); this prefix filter only kicks in for agents
        // with unclassified mutating tools, where the builder falls back to the read-only subset.
        services.AddSingleton<IVoiceToolSafetyInspector, NamingPrefixVoiceToolSafetyInspector>();

        // Voice agent variant builder — produces either the full gated ChatClientAgent (Spec 032,
        // mutating tools wrapped by the approval gate) or, as a fallback, the read-only variant.
        services.AddScoped<IVoiceAgentBuilder, VoiceAgentBuilder>();

        // Frontend tool catalog — server-owned allowlist of tool names the voice
        // model is permitted to call. Stricter than AGUI's client-supplied tools
        // because voice is a persistent authenticated socket.
        services.AddSingleton<IVoiceFrontendToolCatalog, VoiceFrontendToolCatalog>();

        // Per-connection pipeline factory — strategy dispatcher on the tenant's
        // selected VoiceProviderConfiguration. v1 ships chained-OpenAI; other
        // recipes throw a clear configuration error.
        services.AddSingleton<IAonikVoicePipelineFactory, AonikVoicePipelineFactory>();

        // Voice provider configuration validator — used by the admin Update
        // endpoint to reject v1.1 kinds and unwired vendors before persisting.
        services.AddSingleton<IVoiceProviderConfigurationValidator, VoiceProviderConfigurationValidator>();

        // Multi-provider preview engine factory — used by the admin "Test STT/TTS" surface to
        // build a one-shot Voxa engine on demand for any supported vendor without spinning up a
        // full pipeline. Stateless; safe as a singleton.
        services.AddSingleton<IPreviewEngineFactory, PreviewEngineFactory>();

        // Speech provider library (spec 024 Phase A). Built-ins ship in code; tenant-owned rows
        // live in AnkSpeechProviders. The DbContext is module-scoped per AONIK convention but
        // shares the physical database with AonikDbContext via dbo schema + Ank table prefix.
        services.AddSingleton<IBuiltInSpeechCatalog, BuiltInSpeechCatalog>();
        services.AddScoped<ISpeechProviderLibraryService, SpeechProviderLibraryService>();
        services.AddScoped<IVoiceRecipeLibraryService, VoiceRecipeLibraryService>();

        // Singleton-per-tenant active settings (spec 024 Phase C). The runtime cutover
        // (Phase C.2) wires the WSS pipeline + TextToSpeechService to read these.
        services.AddScoped<IVoiceModeSettingsService, VoiceModeSettingsService>();
        services.AddScoped<IChatSpeechSettingsService, ChatSpeechSettingsService>();

        // Unified credential resolver (spec 024 Phase D). Replaces the dual
        // VoiceProviderCredentialSettingsService / TextToSpeechCredentialSettingsService
        // tenant-override storage with a single source of truth — the SpeechProvider row's
        // EncryptedApiKey. Host defaults still come from the existing platform service.
        // Registered concrete-then-bind so all three roles share one instance per scope and
        // we keep its public InvalidateAsync hook callable from the library service.
        services.AddScoped<UnifiedSpeechCredentialResolver>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialResolver>(
            sp => sp.GetRequiredService<UnifiedSpeechCredentialResolver>());
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITextToSpeechCredentialResolver>(
            sp => sp.GetRequiredService<UnifiedSpeechCredentialResolver>());
        services.AddScoped<ISpeechCredentialCacheInvalidator>(
            sp => sp.GetRequiredService<UnifiedSpeechCredentialResolver>());
        services.AddDbContext<VoiceDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"VoiceDb_{Guid.NewGuid()}";
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

        return services;
    }
}

/// <summary>
/// Extension methods for registering the Voice module in the DI container.
/// </summary>
public static class AonikVoiceModuleExtensions
{
    /// <summary>
    /// Adds the Voice module services to the DI container.
    /// Call this from the composition root (Aonik.Api Program.cs).
    /// </summary>
    public static IServiceCollection AddAonikVoiceModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => AonikVoiceModule.ConfigureServices(services, configuration);
}
