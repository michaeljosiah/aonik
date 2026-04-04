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

        if (text.Length > effectiveSettings.Policy.MaxCharactersPerUtterance)
        {
            throw new TextToSpeechPolicyViolationException(
                $"Utterance exceeds the maximum of {effectiveSettings.Policy.MaxCharactersPerUtterance} characters.",
                "max_characters_exceeded");
        }

        if (!_rateLimiter.TryConsume(tenantId, userId, effectiveSettings.Policy.MaxRequestsPerMinutePerUser, out var retryAfter))
        {
            throw new TextToSpeechPolicyViolationException(
                $"Too many text-to-speech requests. Retry after {(int)Math.Ceiling(retryAfter.TotalSeconds)} seconds.",
                "rate_limit_exceeded");
        }

        var provider = ResolveProvider(effectiveSettings.DefaultProfile.Provider);
        var credential = await _credentialResolver.ResolveAsync(effectiveSettings.DefaultProfile.Provider, cancellationToken);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new InvalidOperationException($"Text-to-speech provider '{effectiveSettings.DefaultProfile.Provider}' is not configured.");
        }

        var aiRunId = await _aiRunWriter.StartRunAsync(
            request.UseCase ?? "payabo.chat.tts",
            BuildInputRefsJson(request, effectiveSettings, tenantId, userId),
            cancellationToken);

        try
        {
            var result = await provider.SynthesizeAsync(new TextToSpeechProviderRequest(
                aiRunId,
                tenantId,
                userId,
                text,
                credential.ApiKey,
                effectiveSettings.DefaultProfile.Locale,
                effectiveSettings.DefaultProfile.VoiceId,
                effectiveSettings.DefaultProfile.ModelId,
                effectiveSettings.DefaultProfile.OutputFormat,
                new Dictionary<string, string?>(effectiveSettings.DefaultProfile.ProviderOptions, StringComparer.OrdinalIgnoreCase),
                PreviousText: null,
                NextText: null), cancellationToken);

            await _aiRunWriter.MarkRunCompletedAsync(
                aiRunId,
                outputRef: System.Text.Json.JsonSerializer.Serialize(new
                {
                    provider = result.Provider,
                    voiceId = result.VoiceId,
                    modelId = result.ModelId,
                    contentType = result.ContentType
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
}
