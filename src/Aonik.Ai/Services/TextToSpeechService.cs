using Microsoft.Extensions.Logging;

using Aonik.Ai.Providers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Ai.Services;

internal sealed class TextToSpeechService : ITextToSpeechService
{
    private readonly IEnumerable<ITextToSpeechProvider> _providers;
    private readonly ITextToSpeechCredentialResolver _credentialResolver;
    private readonly ITenantTextToSpeechSettingsService _settingsService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly ITextToSpeechRateLimiter _rateLimiter;
    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(
        IEnumerable<ITextToSpeechProvider> providers,
        ITextToSpeechCredentialResolver credentialResolver,
        ITenantTextToSpeechSettingsService settingsService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAiRunWriter aiRunWriter,
        ITextToSpeechRateLimiter rateLimiter,
        ILogger<TextToSpeechService> logger)
    {
        _providers = providers;
        _credentialResolver = credentialResolver;
        _settingsService = settingsService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _aiRunWriter = aiRunWriter;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<TextToSpeechSynthesisResult> SynthesizeAsync(
        TextToSpeechSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetRequiredUserId();
        var settings = await _settingsService.GetCurrentAsync(cancellationToken);
        var effectiveSettings = ApplyVoiceOverride(settings, request.VoiceProfileOverride, request.Locale);

        if (!effectiveSettings.Enabled)
        {
            throw new TextToSpeechPolicyViolationException("Text-to-speech is disabled for this tenant.", "tts_disabled");
        }

        var text = string.IsNullOrWhiteSpace(request.SpeechText) ? string.Empty : request.SpeechText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new TextToSpeechPolicyViolationException("Speech text is required.", "text_required");
        }

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
