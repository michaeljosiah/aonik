using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

using Aonik.Ai.Providers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Ai.Services;

internal sealed class TextToSpeechService : ITextToSpeechService, IStreamingTextToSpeechService
{
    // 16 KB read buffer matches the typical MP3 frame batch size at
    // 128 kbps and keeps the base64-encoded SSE payload around 21 KB
    // per event — comfortable for HTTP/2 frames and well under any
    // reasonable proxy buffer.
    private const int StreamReadBufferSize = 16 * 1024;

    private readonly IEnumerable<ITextToSpeechProvider> _providers;
    private readonly ITextToSpeechCredentialResolver _credentialResolver;
    private readonly ITenantTextToSpeechSettingsService _settingsService;
    private readonly IChatSpeechSettingsService? _chatSpeechSettings;
    private readonly ISpeechProviderLibraryService? _speechProviderLibrary;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ITextToSpeechRateLimiter _rateLimiter;
    private readonly ITtsCache _ttsCache;
    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(
        IEnumerable<ITextToSpeechProvider> providers,
        ITextToSpeechCredentialResolver credentialResolver,
        ITenantTextToSpeechSettingsService settingsService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAiRunWriter aiRunWriter,
        ITextToSpeechRateLimiter rateLimiter,
        ITtsCache ttsCache,
        ILogger<TextToSpeechService> logger,
        // Spec 024 Phase C.2: optional dependencies so the service can overlay the new
        // singleton-per-tenant ChatSpeechSettings on top of the legacy DefaultProfile.
        // Marked optional so existing test fixtures (which inject the service directly
        // without the speech library wired) keep compiling — the production DI container
        // provides both. When either is null the service silently falls back to legacy.
        IChatSpeechSettingsService? chatSpeechSettings = null,
        ISpeechProviderLibraryService? speechProviderLibrary = null)
    {
        _providers = providers;
        _credentialResolver = credentialResolver;
        _settingsService = settingsService;
        _chatSpeechSettings = chatSpeechSettings;
        _speechProviderLibrary = speechProviderLibrary;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _aiRunWriter = aiRunWriter;
        _rateLimiter = rateLimiter;
        _ttsCache = ttsCache;
        _logger = logger;
    }

    public async Task<TextToSpeechSynthesisResult> SynthesizeAsync(
        TextToSpeechSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetRequiredUserId();
        var settings = await _settingsService.GetCurrentAsync(cancellationToken);
        // Spec 024 Phase C.2: overlay the active Chat Speech setting BEFORE the per-request
        // override so chained precedence is request > tenant chat-speech > legacy default.
        settings = await ApplyChatSpeechOverlayAsync(settings, cancellationToken);
        var effectiveSettings = ApplyVoiceOverride(settings, request.VoiceProfileOverride, request.Locale);

        if (!effectiveSettings.Enabled)
        {
            throw new TextToSpeechPolicyViolationException("Text-to-speech is disabled for this tenant.", "tts_disabled");
        }

        var rawText = string.IsNullOrWhiteSpace(request.SpeechText) ? string.Empty : request.SpeechText.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new TextToSpeechPolicyViolationException("Speech text is required.", "text_required");
        }

        var text = SpeechTextNormalizer.Normalize(rawText);

        if (!_rateLimiter.TryConsume(tenantId, userId, effectiveSettings.Policy.MaxRequestsPerMinutePerUser, out var retryAfter))
        {
            throw new TextToSpeechPolicyViolationException(
                $"Too many text-to-speech requests. Retry after {(int)Math.Ceiling(retryAfter.TotalSeconds)} seconds.",
                "rate_limit_exceeded");
        }

        var provider = ResolveProvider(effectiveSettings.DefaultProfile.Provider);
        var credential = await ResolveRequiredCredentialAsync(effectiveSettings.DefaultProfile.Provider, cancellationToken);

        var aiRunId = await _aiRunWriter.StartRunAsync(
            request.UseCase ?? "payabo.chat.tts",
            BuildInputRefsJson(request, effectiveSettings, tenantId, userId),
            cancellationToken);

        try
        {
            var segments = SplitIntoUtterances(text, effectiveSettings.Policy.MaxCharactersPerUtterance);
            if (segments.Count == 1)
            {
                var result = await provider.SynthesizeAsync(
                    CreateProviderRequest(aiRunId, tenantId, userId, effectiveSettings, credential.ApiKey!, segments, 0),
                    cancellationToken);

                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId,
                    outputRef: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        provider = result.Provider,
                        voiceId = result.VoiceId,
                        modelId = result.ModelId,
                        contentType = result.ContentType,
                        segmentCount = 1
                    }),
                    cancellationToken: cancellationToken);

                return new TextToSpeechSynthesisResult(
                    result.AudioStream,
                    result.ContentType,
                    result.Provider,
                    result.VoiceId,
                    aiRunId,
                    result.ResourceToDispose);
            }

            var combinedAudio = new MemoryStream();
            string? contentType = null;
            string? resultProvider = null;
            string? voiceId = null;
            string? modelId = null;

            for (var index = 0; index < segments.Count; index++)
            {
                var result = await provider.SynthesizeAsync(
                    CreateProviderRequest(aiRunId, tenantId, userId, effectiveSettings, credential.ApiKey!, segments, index),
                    cancellationToken);

                await using var audioStream = result.AudioStream;
                using var resource = result.ResourceToDispose;

                contentType ??= result.ContentType;
                resultProvider ??= result.Provider;
                voiceId ??= result.VoiceId;
                modelId ??= result.ModelId;

                await audioStream.CopyToAsync(combinedAudio, cancellationToken);
            }

            combinedAudio.Position = 0;

            await _aiRunWriter.MarkRunCompletedAsync(
                aiRunId,
                outputRef: System.Text.Json.JsonSerializer.Serialize(new
                {
                    provider = resultProvider,
                    voiceId,
                    modelId,
                    contentType,
                    segmentCount = segments.Count
                }),
                cancellationToken: cancellationToken);

            return new TextToSpeechSynthesisResult(
                combinedAudio,
                contentType ?? "audio/mpeg",
                resultProvider ?? provider.Name,
                voiceId ?? effectiveSettings.DefaultProfile.VoiceId,
                aiRunId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Text-to-speech synthesis failed for tenant {TenantId} user {UserId}", tenantId, userId);
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Streaming variant of <see cref="SynthesizeAsync"/>. Yields audio
    /// frames as they arrive from the provider, applying the same tenant
    /// settings, credential resolution, rate limiting, normalization,
    /// and AiRun audit lifecycle as the buffered path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a cache hit (allowlisted phrase), yields a single
    /// <see cref="TtsAudioFrame"/> with <c>Cached=true</c> and
    /// <c>IsFinal=true</c>; the cached entry's original AiRunId is
    /// surfaced for audit continuity. No new AiRun row is created on a
    /// hit.
    /// </para>
    /// <para>
    /// On a miss, an AiRun is started before the provider call, the
    /// audio stream is read in <see cref="StreamReadBufferSize"/>-byte
    /// windows, and a final empty frame with <c>IsFinal=true</c> is
    /// emitted after the source closes. The AiRun is marked completed on
    /// successful drain or failed if the read or provider call throws.
    /// </para>
    /// </remarks>
    public IAsyncEnumerable<TtsAudioFrame> StreamSynthesizeAsync(
        TextToSpeechSynthesisRequest request,
        CancellationToken cancellationToken = default) =>
        StreamSynthesizeCoreAsync(request, cancellationToken);

    private async IAsyncEnumerable<TtsAudioFrame> StreamSynthesizeCoreAsync(
        TextToSpeechSynthesisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetRequiredUserId();
        var settings = await _settingsService.GetCurrentAsync(cancellationToken);
        // Spec 024 Phase C.2: overlay Chat Speech tenant setting before the per-request override.
        settings = await ApplyChatSpeechOverlayAsync(settings, cancellationToken);
        var effectiveSettings = ApplyVoiceOverride(settings, request.VoiceProfileOverride, request.Locale);

        if (!effectiveSettings.Enabled)
        {
            throw new TextToSpeechPolicyViolationException("Text-to-speech is disabled for this tenant.", "tts_disabled");
        }

        var rawText = string.IsNullOrWhiteSpace(request.SpeechText) ? string.Empty : request.SpeechText.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new TextToSpeechPolicyViolationException("Speech text is required.", "text_required");
        }

        var text = SpeechTextNormalizer.Normalize(rawText);

        if (!_rateLimiter.TryConsume(tenantId, userId, effectiveSettings.Policy.MaxRequestsPerMinutePerUser, out var retryAfter))
        {
            throw new TextToSpeechPolicyViolationException(
                $"Too many text-to-speech requests. Retry after {(int)Math.Ceiling(retryAfter.TotalSeconds)} seconds.",
                "rate_limit_exceeded");
        }

        var providerName = effectiveSettings.DefaultProfile.Provider;
        var configuredVoiceId = effectiveSettings.DefaultProfile.VoiceId;
        var configuredModelId = effectiveSettings.DefaultProfile.ModelId;
        var configuredFormat = effectiveSettings.DefaultProfile.OutputFormat;
        var configuredLocale = effectiveSettings.DefaultProfile.Locale;

        var allowlisted = _ttsCache.IsAllowlisted(text);
        var cacheKey = new TtsCacheKey(
            allowlisted ? TtsCacheAllowlist.HashText(text) : string.Empty,
            tenantId,
            providerName,
            configuredVoiceId,
            configuredModelId,
            configuredFormat,
            configuredLocale);

        if (allowlisted)
        {
            var cached = await _ttsCache.TryGetAsync(cacheKey, cancellationToken);
            if (cached is not null && cached.Audio.Length > 0)
            {
                yield return new TtsAudioFrame(
                    cached.Audio,
                    cached.ContentType,
                    cached.Provider,
                    cached.VoiceId,
                    IsFinal: true,
                    Cached: true,
                    TtsAiRunId: cached.OriginalAiRunId);
                yield break;
            }
        }

        var provider = ResolveProvider(providerName);
        var credential = await ResolveRequiredCredentialAsync(providerName, cancellationToken);

        // Use the existing single-utterance shape — multi-segment chunking is
        // a layer above (the AGUI sentence buffer drives one
        // StreamSynthesizeAsync call per emitted speech.chunk). If the
        // policy max-characters-per-utterance is exceeded for a single
        // chunk, that's a higher-level concern; flagging here keeps the
        // streaming path simple.
        if (effectiveSettings.Policy.MaxCharactersPerUtterance > 0
            && text.Length > effectiveSettings.Policy.MaxCharactersPerUtterance)
        {
            throw new TextToSpeechPolicyViolationException(
                $"Streaming synthesis received text longer than {effectiveSettings.Policy.MaxCharactersPerUtterance} characters; chunk before invoking.",
                "utterance_too_long");
        }

        var aiRunId = await _aiRunWriter.StartRunAsync(
            request.UseCase ?? "payabo.chat.tts.stream",
            BuildInputRefsJson(request, effectiveSettings, tenantId, userId),
            cancellationToken);

        TextToSpeechProviderStreamResult providerResult;
        try
        {
            providerResult = await provider.SynthesizeAsync(
                CreateProviderRequest(aiRunId, tenantId, userId, effectiveSettings, credential.ApiKey!, [text], 0),
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, CancellationToken.None);
            throw;
        }

        var contentType = providerResult.ContentType;
        var providerLabel = providerResult.Provider;
        var providerVoiceId = providerResult.VoiceId;
        var providerModelId = providerResult.ModelId;

        // Buffer audio for cache write-back when allowlisted; null
        // otherwise so user-generated speech is never accumulated in
        // memory for caching. The cache layer also enforces the
        // allowlist so this is defence in depth.
        MemoryStream? cacheBuffer = allowlisted ? new MemoryStream() : null;
        var success = false;
        byte[]? bufferedAudio = null;

        try
        {
            await using (var audioStream = providerResult.AudioStream)
            using (providerResult.ResourceToDispose)
            {
                var buffer = new byte[StreamReadBufferSize];
                var firstFrameEmitted = false;

                while (true)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await audioStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, CancellationToken.None);
                        throw;
                    }

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    cacheBuffer?.Write(buffer, 0, bytesRead);

                    var frameData = new byte[bytesRead];
                    buffer.AsSpan(0, bytesRead).CopyTo(frameData);

                    yield return new TtsAudioFrame(
                        frameData,
                        contentType,
                        providerLabel,
                        providerVoiceId,
                        IsFinal: false,
                        Cached: false,
                        TtsAiRunId: firstFrameEmitted ? null : aiRunId);

                    firstFrameEmitted = true;
                }

                yield return new TtsAudioFrame(
                    ReadOnlyMemory<byte>.Empty,
                    contentType,
                    providerLabel,
                    providerVoiceId,
                    IsFinal: true,
                    Cached: false,
                    TtsAiRunId: firstFrameEmitted ? null : aiRunId);

                if (cacheBuffer is not null)
                {
                    bufferedAudio = cacheBuffer.ToArray();
                }
                success = true;
            }
        }
        finally
        {
            cacheBuffer?.Dispose();

            if (success)
            {
                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId,
                    outputRef: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        provider = providerLabel,
                        voiceId = providerVoiceId,
                        modelId = providerModelId,
                        contentType,
                        streamed = true
                    }),
                    cancellationToken: CancellationToken.None);
            }
            // Failure paths above already invoked MarkRunFailedAsync.
        }

        if (success && allowlisted && bufferedAudio is { Length: > 0 })
        {
            // Best-effort cache write — the synthesis already succeeded
            // for the caller, so a cache write failure must not surface.
            try
            {
                await _ttsCache.SetAsync(
                    cacheKey,
                    new TtsCacheEntry(
                        bufferedAudio,
                        contentType,
                        providerLabel,
                        providerVoiceId,
                        providerModelId,
                        aiRunId,
                        DateTimeOffset.UtcNow),
                    CancellationToken.None);
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(
                    cacheEx,
                    "TTS cache write failed for tenant {TenantId} hash {Hash}",
                    tenantId,
                    cacheKey.TextHash);
            }
        }
    }

    public async Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetCurrentAsync(cancellationToken);
        var providerName = string.IsNullOrWhiteSpace(provider) ? settings.DefaultProfile.Provider : provider!;
        var resolvedProvider = ResolveProvider(providerName);
        var credential = await _credentialResolver.ResolveAsync(providerName, cancellationToken);
        return await resolvedProvider.GetVoicesAsync(credential.ApiKey, cancellationToken);
    }

    public async Task<TextToSpeechVoiceCreationResult> CreateVoiceAsync(
        TextToSpeechVoiceCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerName = string.IsNullOrWhiteSpace(request.Provider)
            ? throw new ArgumentException("Provider is required for voice creation.")
            : request.Provider.Trim();

        var resolvedProvider = ResolveProvider(providerName);
        if (!resolvedProvider.SupportsVoiceCreation)
        {
            throw new InvalidOperationException($"Provider '{providerName}' does not support voice creation.");
        }

        var credential = await ResolveRequiredCredentialAsync(providerName, cancellationToken);

        var providerRequest = new TextToSpeechCreateVoiceRequest(
            request.Name,
            request.SampleAudioBase64,
            request.SampleFilename,
            credential.ApiKey!,
            request.Languages,
            request.Gender,
            request.Age,
            request.Tags);

        var result = await resolvedProvider.CreateVoiceAsync(providerRequest, cancellationToken);

        _logger.LogInformation(
            "Created TTS voice '{VoiceName}' ({VoiceId}) via provider {Provider}",
            result.Name, result.VoiceId, providerName);

        return new TextToSpeechVoiceCreationResult(result.VoiceId, result.Name, providerName);
    }

    public async Task DeleteVoiceAsync(
        string provider,
        string voiceId,
        CancellationToken cancellationToken = default)
    {
        var providerName = string.IsNullOrWhiteSpace(provider)
            ? throw new ArgumentException("Provider is required for voice deletion.")
            : provider.Trim();

        var resolvedProvider = ResolveProvider(providerName);
        var credential = await ResolveRequiredCredentialAsync(providerName, cancellationToken);

        await resolvedProvider.DeleteVoiceAsync(
            new TextToSpeechDeleteVoiceRequest(voiceId, credential.ApiKey),
            cancellationToken);

        _logger.LogInformation(
            "Deleted TTS voice {VoiceId} via provider {Provider}",
            voiceId, providerName);
    }

    /// <summary>
    /// Spec 024 Phase C.2 overlay. Reads the current tenant's
    /// <see cref="ChatSpeechSettings"/> singleton and, if a non-null
    /// <see cref="ChatSpeechSettings.ActiveTtsProviderId"/> resolves to an active TTS
    /// provider in the speech library, replaces <see cref="TextToSpeechSettings.DefaultProfile"/>
    /// with one derived from that provider. Also forces <c>Enabled = false</c> when the user
    /// has explicitly disabled chat speech in the new UI (logical AND with the legacy gate).
    ///
    /// <para>
    /// When the new dependencies aren't wired (test fixtures, lazy bootstrapping), this
    /// method is a no-op — the legacy <see cref="TextToSpeechSettings"/> flows through
    /// unchanged.
    /// </para>
    /// </summary>
    private async Task<TextToSpeechSettings> ApplyChatSpeechOverlayAsync(
        TextToSpeechSettings legacy,
        CancellationToken cancellationToken)
    {
        if (_chatSpeechSettings is null || _speechProviderLibrary is null)
        {
            return legacy;
        }

        ChatSpeechSettings chat;
        try
        {
            chat = await _chatSpeechSettings.GetAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The new path must NEVER take down chat synthesis. If the singleton lookup
            // throws (DB hiccup, misconfiguration), log + fall back to legacy.
            _logger.LogWarning(ex, "Failed to load ChatSpeechSettings; falling back to legacy DefaultProfile.");
            return legacy;
        }

        // Logical AND on Enabled — disabling chat speech in EITHER place kills the feature.
        var working = chat.Enabled ? legacy : legacy with { Enabled = false };

        if (string.IsNullOrEmpty(chat.ActiveTtsProviderId))
        {
            return working;
        }

        SpeechProvider? sp;
        try
        {
            sp = await _speechProviderLibrary.GetAsync(chat.ActiveTtsProviderId!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve ChatSpeech ActiveTtsProviderId {Id}; falling back to legacy DefaultProfile.",
                chat.ActiveTtsProviderId);
            return working;
        }

        if (sp is null || sp.Type != SpeechProviderType.Tts || sp.Status != SpeechProviderStatus.Active)
        {
            // The selected provider was disabled / deleted / typed-wrong since chat speech
            // was configured. Log loudly so the admin notices in observability and fall
            // back to legacy so the chat keeps speaking.
            _logger.LogWarning(
                "ChatSpeech ActiveTtsProviderId {Id} no longer resolves to an active TTS provider (resolved={Resolved}); falling back to legacy DefaultProfile.",
                chat.ActiveTtsProviderId,
                sp is null ? "null" : $"{sp.DisplayName} type={sp.Type} status={sp.Status}");
            return working;
        }

        // Voice id moved off the provider config (Phase D refactor) — read it from the
        // singleton chat-speech row. If it's missing the validator should have caught the
        // pairing rule already; we still defend with a fall-back.
        if (string.IsNullOrWhiteSpace(chat.ActiveTtsVoiceId))
        {
            _logger.LogWarning(
                "ChatSpeech ActiveTtsProviderId {Id} is set but ActiveTtsVoiceId is empty; falling back to legacy DefaultProfile.",
                chat.ActiveTtsProviderId);
            return working;
        }

        var overlaidProfile = TryBuildProfileFromSpeechProvider(
            working.DefaultProfile,
            sp,
            chat.ActiveTtsVoiceId!,
            chat.ActiveTtsModelId);
        if (overlaidProfile is null)
        {
            // We don't have an engine wired for this config kind yet (e.g. OpenAI TTS in
            // the chat path which only ships ElevenLabs + Mistral providers). Log + leave
            // legacy in place — the existing `Provider not registered` failure mode will
            // surface naturally if/when synthesis is attempted with an unsupported config.
            _logger.LogWarning(
                "ChatSpeech ActiveTtsProvider {Id} uses config kind {Kind} which has no overlay mapping; falling back to legacy DefaultProfile.",
                sp.Id,
                sp.Config.GetType().Name);
            return working;
        }

        return working with { DefaultProfile = overlaidProfile };
    }

    /// <summary>
    /// Maps a <see cref="SpeechProvider.Config"/> + the per-tenant voice/model picks from
    /// <see cref="ChatSpeechSettings"/> into the legacy <see cref="TextToSpeechVoiceProfile"/>
    /// shape. Returns null when the config kind has no chat-path engine wired (callers fall
    /// back to the legacy profile).
    /// </summary>
    private static TextToSpeechVoiceProfile? TryBuildProfileFromSpeechProvider(
        TextToSpeechVoiceProfile fallback,
        SpeechProvider sp,
        string voiceId,
        string? modelId)
    {
        return sp.Config switch
        {
            ElevenLabsTtsConfig el => new TextToSpeechVoiceProfile(
                Provider: "ElevenLabs",
                VoiceId: voiceId,
                ModelId: ResolveModelId(modelId, el.DefaultModelId, fallback.ModelId),
                Locale: fallback.Locale,
                OutputFormat: fallback.OutputFormat,
                ProviderOptions: BuildElevenLabsOptions(el)),
            MistralTtsConfig m => new TextToSpeechVoiceProfile(
                Provider: "Mistral",
                VoiceId: voiceId,
                ModelId: ResolveModelId(modelId, m.DefaultModelId, fallback.ModelId),
                Locale: fallback.Locale,
                OutputFormat: fallback.OutputFormat,
                ProviderOptions: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)),
            _ => null,
        };
    }

    /// <summary>
    /// Three-step model id resolution: per-tenant override → provider's default → legacy
    /// fallback profile. Used so admins can set a vendor-wide default model on the provider
    /// row and only override per chat-speech setting when needed.
    /// </summary>
    private static string? ResolveModelId(string? perTenantOverride, string? providerDefault, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(perTenantOverride)) return perTenantOverride!;
        if (!string.IsNullOrWhiteSpace(providerDefault)) return providerDefault!;
        return fallback;
    }

    /// <summary>
    /// Translates the typed <see cref="ElevenLabsTtsConfig"/> tunables into the
    /// string-keyed dictionary the existing
    /// <c>ElevenLabsTextToSpeechProvider</c> already understands.
    /// </summary>
    private static Dictionary<string, string?> BuildElevenLabsOptions(ElevenLabsTtsConfig el)
    {
        var opts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (el.DefaultStability.HasValue) opts["stability"] = el.DefaultStability.Value.ToString(inv);
        if (el.DefaultSimilarityBoost.HasValue) opts["similarityBoost"] = el.DefaultSimilarityBoost.Value.ToString(inv);
        if (el.DefaultOptimizeStreamingLatency.HasValue) opts["optimizeStreamingLatency"] = el.DefaultOptimizeStreamingLatency.Value.ToString(inv);
        return opts;
    }

    private static TextToSpeechSettings ApplyVoiceOverride(
        TextToSpeechSettings settings,
        TextToSpeechVoiceProfile? overrideProfile,
        string? requestLocale)
    {
        if (overrideProfile == null && string.IsNullOrWhiteSpace(requestLocale))
        {
            return settings;
        }

        var profile = settings.DefaultProfile;
        if (overrideProfile != null)
        {
            profile = new TextToSpeechVoiceProfile(
                string.IsNullOrWhiteSpace(overrideProfile.Provider) ? profile.Provider : overrideProfile.Provider.Trim(),
                string.IsNullOrWhiteSpace(overrideProfile.VoiceId) ? profile.VoiceId : overrideProfile.VoiceId.Trim(),
                string.IsNullOrWhiteSpace(overrideProfile.ModelId) ? profile.ModelId : overrideProfile.ModelId.Trim(),
                string.IsNullOrWhiteSpace(overrideProfile.Locale) ? profile.Locale : overrideProfile.Locale.Trim(),
                string.IsNullOrWhiteSpace(overrideProfile.OutputFormat) ? profile.OutputFormat : overrideProfile.OutputFormat.Trim(),
                overrideProfile.ProviderOptions.Count == 0
                    ? new Dictionary<string, string?>(profile.ProviderOptions, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(overrideProfile.ProviderOptions, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(requestLocale))
        {
            profile = profile with { Locale = requestLocale.Trim() };
        }

        return settings with { DefaultProfile = profile };
    }

    private Guid GetRequiredUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId) || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication required.");
        }

        return userId;
    }

    private async Task<TextToSpeechProviderCredentialResolution> ResolveRequiredCredentialAsync(
        string providerName,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialResolver.ResolveAsync(providerName, cancellationToken);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new InvalidOperationException($"Text-to-speech provider '{providerName}' is not configured.");
        }

        return credential;
    }

    private ITextToSpeechProvider ResolveProvider(string providerName)
    {
        var provider = _providers.FirstOrDefault(item => string.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            throw new InvalidOperationException($"Text-to-speech provider '{providerName}' is not registered.");
        }

        return provider;
    }

    private static string BuildInputRefsJson(
        TextToSpeechSynthesisRequest request,
        TextToSpeechSettings settings,
        Guid tenantId,
        Guid userId)
    {
        var normalizedText = string.IsNullOrWhiteSpace(request.SpeechText) ? string.Empty : request.SpeechText.Trim();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedText)));

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            tenantId,
            userId,
            threadId = request.ThreadId,
            messageId = request.MessageId,
            locale = request.Locale,
            provider = settings.DefaultProfile.Provider,
            voiceId = settings.DefaultProfile.VoiceId,
            modelId = settings.DefaultProfile.ModelId,
            textHash = hash,
            textLength = normalizedText.Length
        });
    }

    private static TextToSpeechProviderRequest CreateProviderRequest(
        Guid aiRunId,
        Guid tenantId,
        Guid userId,
        TextToSpeechSettings settings,
        string apiKey,
        IReadOnlyList<string> segments,
        int index)
    {
        return new TextToSpeechProviderRequest(
            aiRunId,
            tenantId,
            userId,
            segments[index],
            apiKey,
            settings.DefaultProfile.Locale,
            settings.DefaultProfile.VoiceId,
            settings.DefaultProfile.ModelId,
            settings.DefaultProfile.OutputFormat,
            new Dictionary<string, string?>(settings.DefaultProfile.ProviderOptions, StringComparer.OrdinalIgnoreCase),
            PreviousText: index > 0 ? segments[index - 1] : null,
            NextText: index < segments.Count - 1 ? segments[index + 1] : null);
    }

    internal static IReadOnlyList<string> SplitIntoUtterances(string text, int maxCharactersPerUtterance)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (maxCharactersPerUtterance <= 0 || text.Length <= maxCharactersPerUtterance)
        {
            return [text];
        }

        var remaining = text;
        var segments = new List<string>();

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            if (remaining.Length <= maxCharactersPerUtterance)
            {
                segments.Add(remaining.Trim());
                break;
            }

            var splitIndex = FindSplitIndex(remaining, maxCharactersPerUtterance);
            segments.Add(remaining[..splitIndex].Trim());
            remaining = remaining[splitIndex..].TrimStart();
        }

        return segments;
    }

    private static int FindSplitIndex(string text, int maxCharactersPerUtterance)
    {
        var sentenceBoundary = text.LastIndexOfAny(['.', '!', '?'], maxCharactersPerUtterance - 1, maxCharactersPerUtterance);
        if (sentenceBoundary >= 0)
        {
            return sentenceBoundary + 1;
        }

        var phraseBoundary = text.LastIndexOfAny([',', ';', ':'], maxCharactersPerUtterance - 1, maxCharactersPerUtterance);
        if (phraseBoundary >= 0)
        {
            return phraseBoundary + 1;
        }

        var whitespaceBoundary = text.LastIndexOf(' ', maxCharactersPerUtterance - 1, maxCharactersPerUtterance);
        if (whitespaceBoundary >= 0)
        {
            return whitespaceBoundary;
        }

        return maxCharactersPerUtterance;
    }
}
