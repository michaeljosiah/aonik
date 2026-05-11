using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.Voice.Pipeline;
using Aonik.Voice.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxa.Pipelines;

namespace Aonik.Voice.Endpoints;

/// <summary>
/// <c>WSS /ai/voice</c> handler. Owns WebSocket lifecycle, hello validation,
/// and per-connection setup; delegates the actual voice pipeline to
/// <see cref="IAonikVoicePipelineFactory"/>.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> "Endpoint Lifecycle".
/// </para>
/// </summary>
internal static class VoiceWebSocketEndpoint
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var services = context.RequestServices;
        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aonik.Voice.VoiceWebSocketEndpoint");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "WebSocket upgrade required." },
                cancellationToken);
            return;
        }

        // Authentication has already validated the JWT and OnTokenValidated
        // populated ITenantContext + ICurrentUserContext on the request scope.
        // The MobileVoicePolicy authorization check fired via the route's
        // RequireAuthorization metadata before we reached this handler.

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Voice WebSocket accepted");

        try
        {
            HelloEnvelope hello;
            try
            {
                hello = await HelloEnvelopeReader.ReadAsync(socket, cancellationToken);
            }
            catch (HelloParseException ex)
            {
                logger.LogWarning(ex, "Voice WebSocket: rejected hello envelope");
                await SendErrorAsync(socket, "hello-invalid", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Invalid hello envelope", cancellationToken);
                return;
            }

            // Validate frontend tools against the server-owned allowlist.
            var catalog = services.GetRequiredService<IVoiceFrontendToolCatalog>();
            var rejected = catalog.Validate(hello.FrontendTools ?? new List<string>());
            if (rejected.Count > 0)
            {
                var message = $"Unknown frontend tool name(s): {string.Join(", ", rejected)}";
                logger.LogWarning("Voice WebSocket: {Message}", message);
                await SendErrorAsync(socket, "frontend-tool-unknown", message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Unknown frontend tool(s)", cancellationToken);
                return;
            }

            // Resolve the tenant's active Voice Mode recipe (spec 024 Phase C.2).
            // The legacy ITenantVoiceProviderSettingsService is no longer the runtime source
            // of truth — this endpoint now reads VoiceModeSettings (which recipe is active)
            // and resolves the recipe + its provider refs from the speech library.
            var voiceMode = services.GetRequiredService<IVoiceModeSettingsService>();
            var voiceModeSettings = await voiceMode.GetAsync(cancellationToken);
            if (!voiceModeSettings.Enabled)
            {
                await SendErrorAsync(socket, "voice-not-configured", "Voice mode is disabled for this tenant.", cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice mode disabled", cancellationToken);
                return;
            }
            if (string.IsNullOrEmpty(voiceModeSettings.ActiveRecipeId))
            {
                await SendErrorAsync(socket, "voice-no-recipe", "No voice recipe is selected. Pick a recipe in Settings → Speech & Voice → Voice mode.", cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "No active recipe", cancellationToken);
                return;
            }

            ResolvedRecipe resolvedRecipe;
            try
            {
                resolvedRecipe = await ResolveRecipeAsync(services, voiceModeSettings.ActiveRecipeId!, cancellationToken);
            }
            catch (VoiceConfigurationException ex)
            {
                logger.LogWarning(ex, "Voice WebSocket: recipe resolution failed");
                await SendErrorAsync(socket, "voice-config-invalid", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice recipe error", cancellationToken);
                return;
            }

            // Resolve agent + descriptor + user-brief preamble.
            var contextualizer = services.GetRequiredService<IAgentContextualizer>();
            var domainResolver = services.GetRequiredService<IDomainAgentResolver>();
            var agentContext = await contextualizer.ResolveAsync(hello.AgentId, cancellationToken);
            var domainResolution = await domainResolver.ResolveAsync(hello.AgentId!, cancellationToken);

            // Phase 1.5: build the read-only voice variant of the resolved agent.
            // Original agent (used by AGUI) is unchanged.
            var voiceBuilder = services.GetRequiredService<IVoiceAgentBuilder>();
            var voiceAgentResult = voiceBuilder.BuildReadOnlyVariant(domainResolution.Descriptor, services);

            // Build run options. Same call shape as AguiStreamPipeline:74.
            var runOptionsBuilder = services.GetRequiredService<IAguiRunOptionsBuilder>();
            // v1: clientTools come from the server-owned canonical catalog, NOT from
            // the hello envelope. The hello list is used purely as a capability
            // negotiation (the server only declares tools the client claims it can
            // render). Once the catalog ships canonical AITool declarations
            // (Phase 3 follow-up), this will populate.
            var clientTools = catalog.ResolveCanonical(hello.FrontendTools ?? new List<string>());
            var runOptions = runOptionsBuilder.Build(clientTools, agentContext.ConfiguredModelName);

            // Stamp use_case = "voice" on ChatOptions.AdditionalProperties so
            // TelemetryChatClient tags AI run logs correctly. Mirrors the AGUI
            // path (AguiStreamingEndpoint.cs:111 sets the activity tag; here we
            // also set the property because run options carry it through to
            // TelemetryChatClient).
            runOptions ??= new Microsoft.Agents.AI.ChatClientAgentRunOptions { ChatOptions = new ChatOptions() };
            runOptions.ChatOptions ??= new ChatOptions();
            runOptions.ChatOptions.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            runOptions.ChatOptions.AdditionalProperties[AiTelemetry.UseCaseAttribute] = "voice";

            // Resolve tenant + user IDs from the request scope (set by
            // OnTokenValidated → tenantContext / currentUserContext).
            Guid? tenantId = TryGetTenantId(services);
            Guid? userId = TryGetUserId(services);

            // Compose the pipeline. Chained vs composite is the one fork in the runtime —
            // each shape has its own builder + runtime spec.
            var factory = services.GetRequiredService<IAonikVoicePipelineFactory>();
            var buildRequest = new VoicePipelineBuildRequest(
                WebSocket: socket,
                VoiceAgent: voiceAgentResult.Agent,
                UserBriefPreamble: agentContext.UserBriefPreamble,
                RunOptions: runOptions,
                FrontendToolNames: catalog.AllowedNames,
                InitialChatThreadId: hello.ChatThreadId,
                AgentId: hello.AgentId,
                TenantId: tenantId,
                UserId: userId,
                RequestServices: services);

            Voxa.Pipelines.Pipeline pipeline;
            try
            {
                pipeline = resolvedRecipe switch
                {
                    ResolvedRecipe.Chained chained => factory.BuildChained(buildRequest, chained.Spec),
                    ResolvedRecipe.Composite composite => factory.BuildComposite(buildRequest, composite.Spec),
                    _ => throw new InvalidOperationException("Unreachable: ResolvedRecipe is sealed."),
                };
            }
            catch (VoiceConfigurationException ex)
            {
                logger.LogWarning(ex, "Voice WebSocket: pipeline configuration error");
                await SendErrorAsync(socket, "voice-config-invalid", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice configuration error", cancellationToken);
                return;
            }

            // Connection-level AiRun (spec 024 Phase E**). One row per voice connection so
            // the AI dashboard can chart who's using voice mode, with which recipe, for how
            // long, and how it ended. Per-LLM-turn AiRuns continue to flow through
            // TelemetryChatClient (use case "voice", set on ChatOptions above) — those track
            // tokens / cost. This row tracks the *session*.
            //
            // StartRunAsync also runs the tenant kill-switch check, so engaging the kill
            // switch immediately blocks new voice connections without us needing a separate
            // gate here.
            var aiRunWriter = services.GetRequiredService<IAiRunWriter>();
            var voiceSessionInputRefs = JsonSerializer.Serialize(
                resolvedRecipe.ToAiRunInputRefs(hello.AgentId, hello.ChatThreadId),
                JsonOpts);

            Guid? voiceRunId = null;
            try
            {
                voiceRunId = await aiRunWriter.StartRunAsync(
                    useCase: "voice-session",
                    inputRefsJson: voiceSessionInputRefs,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Kill-switch or any other gate failure — surface to client and bail.
                // Catching the broad Exception keeps the endpoint independent of the
                // Aonik.Ai.Services.KillSwitchEngagedException type while still surfacing
                // the message verbatim for admin debugging.
                logger.LogWarning(ex, "Voice WebSocket: AiRun start refused (kill-switch or gate)");
                await SendErrorAsync(socket, "voice-start-blocked", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice run start blocked", cancellationToken);
                return;
            }

            await using var runner = new PipelineRunner(pipeline, cancellationToken);
            await runner.StartAsync(ct: cancellationToken);

            var sessionStartedAtUtc = DateTime.UtcNow;
            try
            {
                // PipelineRunner.WaitAsync completes when the sink observes EndFrame
                // (graceful shutdown) or an upstream ErrorFrame surfaces. Cancellation
                // happens via the runner's external token (set in the constructor),
                // so we just await — Task.WaitAsync wraps the cancellation timeout
                // for safety.
                await runner.WaitAsync().WaitAsync(cancellationToken);

                // Graceful end — record duration as latencyMs so dashboards can chart
                // session-length distributions. Tokens / cost stay 0 here; per-turn
                // telemetry already counts those separately.
                var durationMs = ClampToInt32Ms(DateTime.UtcNow - sessionStartedAtUtc);
                await aiRunWriter.MarkRunCompletedWithMetricsAsync(
                    voiceRunId.Value,
                    tokensUsed: 0,
                    latencyMs: durationMs,
                    costEstimate: 0m,
                    outputRef: null,
                    CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected — count as a clean completion (the user simply
                // hung up). We still record duration so dashboards can spot suspicious
                // mid-session disconnects via the latency distribution.
                var durationMs = ClampToInt32Ms(DateTime.UtcNow - sessionStartedAtUtc);
                try
                {
                    await aiRunWriter.MarkRunCompletedWithMetricsAsync(
                        voiceRunId.Value,
                        tokensUsed: 0,
                        latencyMs: durationMs,
                        costEstimate: 0m,
                        outputRef: "client-disconnect",
                        CancellationToken.None);
                }
                catch (Exception markEx)
                {
                    logger.LogWarning(markEx, "Voice WebSocket: failed to record session-end AiRun on cancel");
                }
            }
            catch (Exception ex)
            {
                // Pipeline error mid-session. Record as Failed so dashboards see it
                // separately from clean closes; the outer catch (Exception) will still
                // log + close the socket.
                try
                {
                    await aiRunWriter.MarkRunFailedAsync(voiceRunId.Value, ex.Message, CancellationToken.None);
                }
                catch (Exception markEx)
                {
                    logger.LogWarning(markEx, "Voice WebSocket: failed to record session-failed AiRun");
                }
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — normal.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice WebSocket: unhandled exception");
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await SendErrorAsync(socket, "server-error", "Server error", default);
                    await CloseAsync(socket, WebSocketCloseStatus.InternalServerError, "Server error", default);
                }
                catch
                {
                    // Best-effort.
                }
            }
        }
    }

    /// <summary>
    /// Tagged union of resolved recipe shapes. Wraps the kind-specific runtime spec so
    /// the endpoint can dispatch to <see cref="IAonikVoicePipelineFactory.BuildChained"/>
    /// or <see cref="IAonikVoicePipelineFactory.BuildComposite"/> without leaking each
    /// shape's fields into the surrounding code.
    /// </summary>
    private abstract record ResolvedRecipe
    {
        public sealed record Chained(ChainedRecipeRuntimeSpec Spec) : ResolvedRecipe;
        public sealed record Composite(CompositeRecipeRuntimeSpec Spec) : ResolvedRecipe;

        public object ToAiRunInputRefs(string? agentId, string? chatThreadId) => this switch
        {
            Chained c => new
            {
                recipeId = c.Spec.RecipeId,
                recipeName = c.Spec.RecipeDisplayName,
                sttProvider = c.Spec.SttProviderDisplayName,
                ttsProvider = c.Spec.TtsProviderDisplayName,
                kind = "chained",
                agentId,
                chatThreadId,
            },
            Composite c => new
            {
                recipeId = c.Spec.RecipeId,
                recipeName = c.Spec.RecipeDisplayName,
                compositeProvider = c.Spec.ProviderDisplayName,
                voice = c.Spec.Voice,
                model = c.Spec.Model,
                kind = "composite",
                agentId,
                chatThreadId,
            },
            _ => throw new InvalidOperationException("Unreachable: ResolvedRecipe is sealed."),
        };
    }

    /// <summary>
    /// Pulls the named recipe from the speech library, validates it's active, and resolves
    /// its referenced providers into a fully-typed runtime spec the factory can consume.
    /// Chained and Composite both supported. Each failure mode throws
    /// <see cref="VoiceConfigurationException"/> with a message safe to surface to the
    /// client.
    /// </summary>
    private static async Task<ResolvedRecipe> ResolveRecipeAsync(
        IServiceProvider services,
        string recipeId,
        CancellationToken ct)
    {
        var recipes = services.GetRequiredService<IVoiceRecipeLibraryService>();
        var providers = services.GetRequiredService<ISpeechProviderLibraryService>();

        var recipe = await recipes.GetAsync(recipeId, ct);
        if (recipe is null)
        {
            throw new VoiceConfigurationException(
                $"Voice recipe '{recipeId}' was not found in this tenant's library. Pick a recipe in Settings → Speech & Voice → Voice mode.");
        }
        if (recipe.Status != VoiceRecipeStatus.Active)
        {
            throw new VoiceConfigurationException(
                $"Voice recipe '{recipe.DisplayName}' is not active. Re-enable it or pick a different recipe.");
        }

        return recipe.Kind switch
        {
            VoiceRecipeKind.Chained => new ResolvedRecipe.Chained(
                await ResolveChainedSpecAsync(recipe, providers, ct)),
            VoiceRecipeKind.Composite => new ResolvedRecipe.Composite(
                await ResolveCompositeSpecAsync(recipe, providers, ct)),
            _ => throw new VoiceConfigurationException(
                $"Voice recipe '{recipe.DisplayName}' has unknown kind '{recipe.Kind}'."),
        };
    }

    private static async Task<ChainedRecipeRuntimeSpec> ResolveChainedSpecAsync(
        VoiceRecipe recipe,
        ISpeechProviderLibraryService providers,
        CancellationToken ct)
    {
        if (recipe.Chained is null)
        {
            throw new VoiceConfigurationException(
                $"Voice recipe '{recipe.DisplayName}' is marked Chained but has no chained body.");
        }

        var stt = await providers.GetAsync(recipe.Chained.SttProviderId, ct)
            ?? throw new VoiceConfigurationException(
                $"STT provider '{recipe.Chained.SttProviderId}' referenced by recipe '{recipe.DisplayName}' was not found.");
        if (stt.Type != SpeechProviderType.Stt)
        {
            throw new VoiceConfigurationException(
                $"Provider '{stt.DisplayName}' referenced as STT by recipe '{recipe.DisplayName}' is type {stt.Type}, expected Stt.");
        }
        if (stt.Status != SpeechProviderStatus.Active)
        {
            throw new VoiceConfigurationException(
                $"STT provider '{stt.DisplayName}' is not active. Re-enable it or pick a different provider.");
        }

        var tts = await providers.GetAsync(recipe.Chained.TtsProviderId, ct)
            ?? throw new VoiceConfigurationException(
                $"TTS provider '{recipe.Chained.TtsProviderId}' referenced by recipe '{recipe.DisplayName}' was not found.");
        if (tts.Type != SpeechProviderType.Tts)
        {
            throw new VoiceConfigurationException(
                $"Provider '{tts.DisplayName}' referenced as TTS by recipe '{recipe.DisplayName}' is type {tts.Type}, expected Tts.");
        }
        if (tts.Status != SpeechProviderStatus.Active)
        {
            throw new VoiceConfigurationException(
                $"TTS provider '{tts.DisplayName}' is not active. Re-enable it or pick a different provider.");
        }

        return new ChainedRecipeRuntimeSpec(
            RecipeId: recipe.Id,
            RecipeDisplayName: recipe.DisplayName,
            SttProviderDisplayName: stt.DisplayName,
            TtsProviderDisplayName: tts.DisplayName,
            SttConfig: stt.Config,
            TtsConfig: tts.Config,
            TtsVoiceId: recipe.Chained.TtsVoiceId,
            TtsModelId: recipe.Chained.TtsModelId,
            SttModel: recipe.Chained.SttModel,
            SttLanguage: recipe.Chained.SttLanguage,
            UseSentenceAggregator: recipe.Chained.SentenceAggregator,
            Vad: recipe.Chained.Vad,
            VadStopMs: recipe.Chained.VadStopMs);
    }

    private static async Task<CompositeRecipeRuntimeSpec> ResolveCompositeSpecAsync(
        VoiceRecipe recipe,
        ISpeechProviderLibraryService providers,
        CancellationToken ct)
    {
        if (recipe.Composite is null)
        {
            throw new VoiceConfigurationException(
                $"Voice recipe '{recipe.DisplayName}' is marked Composite but has no composite body.");
        }

        var provider = await providers.GetAsync(recipe.Composite.CompositeProviderId, ct)
            ?? throw new VoiceConfigurationException(
                $"Composite provider '{recipe.Composite.CompositeProviderId}' referenced by recipe '{recipe.DisplayName}' was not found.");
        if (provider.Type != SpeechProviderType.Composite)
        {
            throw new VoiceConfigurationException(
                $"Provider '{provider.DisplayName}' referenced as Composite by recipe '{recipe.DisplayName}' is type {provider.Type}, expected Composite.");
        }
        if (provider.Status != SpeechProviderStatus.Active)
        {
            throw new VoiceConfigurationException(
                $"Composite provider '{provider.DisplayName}' is not active. Re-enable it or pick a different provider.");
        }

        return new CompositeRecipeRuntimeSpec(
            RecipeId: recipe.Id,
            RecipeDisplayName: recipe.DisplayName,
            ProviderDisplayName: provider.DisplayName,
            ProviderConfig: provider.Config,
            Voice: recipe.Composite.Voice,
            Model: recipe.Composite.Model,
            InstructionsAddendum: recipe.Composite.InstructionsAddendum);
    }

    private static Guid? TryGetTenantId(IServiceProvider services)
    {
        var tenantContext = services.GetService<ITenantContext>();
        if (tenantContext?.TenantId is { } id && id != Guid.Empty) return id;
        var tenantProvider = services.GetService<ITenantProvider>();
        if (tenantProvider is not null && tenantProvider.TryGetCurrentTenantId(out var providerId))
            return providerId;
        return null;
    }

    private static Guid? TryGetUserId(IServiceProvider services)
    {
        var userContext = services.GetService<ICurrentUserContext>();
        if (userContext?.UserId is { } id && id != Guid.Empty) return id;
        var currentUserProvider = services.GetService<ICurrentUserProvider>();
        if (currentUserProvider is not null && currentUserProvider.TryGetCurrentUserId(out var providerId))
            return providerId;
        return null;
    }

    private static Task SendErrorAsync(System.Net.WebSockets.WebSocket socket, string code, string message, CancellationToken ct)
        => SendEnvelopeAsync(socket, new { type = "error", code, message }, ct);

    private static async Task SendEnvelopeAsync(System.Net.WebSockets.WebSocket socket, object envelope, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static async Task CloseAsync(System.Net.WebSockets.WebSocket socket, WebSocketCloseStatus status, string description, CancellationToken ct)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(status, description, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clamp a session duration to a non-negative <c>int</c> millisecond value for the
    /// <see cref="IAiRunWriter.MarkRunCompletedWithMetricsAsync"/> latency field. Voice
    /// sessions can run hours, but <see cref="int"/> still tops out at ~24.8 days, so we
    /// clamp rather than throw — a wrapped value would be far more confusing.
    /// </summary>
    private static int ClampToInt32Ms(TimeSpan elapsed)
    {
        var totalMs = elapsed.TotalMilliseconds;
        if (totalMs <= 0) return 0;
        if (totalMs >= int.MaxValue) return int.MaxValue;
        return (int)totalMs;
    }
}
