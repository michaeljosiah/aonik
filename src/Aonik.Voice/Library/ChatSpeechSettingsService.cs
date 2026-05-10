using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Entities;
using Aonik.Voice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Library;

/// <summary>
/// Reads/writes <see cref="ChatSpeechSettings"/> for the current tenant. Singleton-per-tenant
/// row keyed by tenant id; first write inserts. Validates the referenced TTS provider on every
/// update so a stale id can't sneak past the form.
/// </summary>
internal sealed class ChatSpeechSettingsService : IChatSpeechSettingsService
{
    /// <summary>Inclusive lower bound for <see cref="ChatSpeechSettings.RatePercent"/>.</summary>
    public const int MinRatePercent = 50;

    /// <summary>Inclusive upper bound for <see cref="ChatSpeechSettings.RatePercent"/>.</summary>
    public const int MaxRatePercent = 200;

    private readonly VoiceDbContext _db;
    private readonly ISpeechProviderLibraryService _providers;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly IClock _clock;

    public ChatSpeechSettingsService(
        VoiceDbContext db,
        ISpeechProviderLibraryService providers,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        IClock clock)
    {
        _db = db;
        _providers = providers;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    public async Task<ChatSpeechSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await LoadAsync(cancellationToken);
        return entity is null ? Defaults() : ToDto(entity);
    }

    public async Task<ChatSpeechSettings> UpdateAsync(
        UpdateChatSpeechSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRate(request.RatePercent);
        await ValidateProviderReferenceAsync(request.ActiveTtsProviderId, cancellationToken);
        ValidateVoiceConsistency(request.ActiveTtsProviderId, request.ActiveTtsVoiceId);

        var existing = await LoadAsync(cancellationToken);
        if (existing is null)
        {
            existing = new ChatSpeechSettingsEntity
            {
                TenantId = _tenant.GetCurrentTenantId(),
                ActiveTtsProviderId = request.ActiveTtsProviderId,
                ActiveTtsVoiceId = request.ActiveTtsVoiceId,
                ActiveTtsModelId = request.ActiveTtsModelId,
                Enabled = request.Enabled,
                AutoPlay = request.AutoPlay,
                ShowSpeakButton = request.ShowSpeakButton,
                RatePercent = request.RatePercent,
            };
            _db.ChatSpeechSettings.Add(existing);
        }
        else
        {
            existing.ActiveTtsProviderId = request.ActiveTtsProviderId;
            existing.ActiveTtsVoiceId = request.ActiveTtsVoiceId;
            existing.ActiveTtsModelId = request.ActiveTtsModelId;
            existing.Enabled = request.Enabled;
            existing.AutoPlay = request.AutoPlay;
            existing.ShowSpeakButton = request.ShowSpeakButton;
            existing.RatePercent = request.RatePercent;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private static void ValidateVoiceConsistency(string? providerId, string? voiceId)
    {
        // Pairing rule: if a provider is selected, a voice must be too. Voice without
        // provider doesn't make sense; the form should keep them in lockstep but we enforce
        // server-side as defence in depth.
        if (!string.IsNullOrEmpty(providerId) && string.IsNullOrWhiteSpace(voiceId))
        {
            throw new SpeechLibraryValidationException(
                "ActiveTtsVoiceId is required when ActiveTtsProviderId is set.",
                fieldName: nameof(UpdateChatSpeechSettingsRequest.ActiveTtsVoiceId));
        }
    }

    private async Task<ChatSpeechSettingsEntity?> LoadAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetCurrentTenantId();
        return await _db.ChatSpeechSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
    }

    private async Task ValidateProviderReferenceAsync(string? providerId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(providerId)) return;

        var provider = await _providers.GetAsync(providerId, ct);
        if (provider is null)
        {
            throw new SpeechLibraryValidationException(
                $"TTS provider '{providerId}' was not found in this tenant's library.");
        }

        if (provider.Type != SpeechProviderType.Tts)
        {
            throw new SpeechLibraryValidationException(
                $"Provider '{provider.DisplayName}' is type {provider.Type}, expected Tts for Chat Speech.");
        }

        if (provider.Status != SpeechProviderStatus.Active)
        {
            throw new SpeechLibraryValidationException(
                $"Provider '{provider.DisplayName}' is not active and can't drive Chat Speech.");
        }
    }

    private static void ValidateRate(int rate)
    {
        if (rate < MinRatePercent || rate > MaxRatePercent)
        {
            throw new SpeechLibraryValidationException(
                $"RatePercent must be between {MinRatePercent} and {MaxRatePercent}; got {rate}.");
        }
    }

    private ChatSpeechSettings Defaults() => new(
        ActiveTtsProviderId: null,
        ActiveTtsVoiceId: null,
        ActiveTtsModelId: null,
        Enabled: true,
        AutoPlay: false,
        ShowSpeakButton: true,
        RatePercent: 100,
        UpdatedAt: _clock.UtcNow,
        LastUpdatedByUserId: null);

    private static ChatSpeechSettings ToDto(ChatSpeechSettingsEntity e) => new(
        ActiveTtsProviderId: e.ActiveTtsProviderId,
        ActiveTtsVoiceId: e.ActiveTtsVoiceId,
        ActiveTtsModelId: e.ActiveTtsModelId,
        Enabled: e.Enabled,
        AutoPlay: e.AutoPlay,
        ShowSpeakButton: e.ShowSpeakButton,
        RatePercent: e.RatePercent,
        UpdatedAt: e.UpdatedAt ?? e.CreatedAt,
        LastUpdatedByUserId: e.UpdatedBy ?? e.CreatedBy);
}
