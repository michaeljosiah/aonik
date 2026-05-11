using System.Text.Json;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Entities;
using Aonik.Voice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Library;

/// <summary>
/// Per-tenant CRUD over the voice recipe library. Built-in recipes are merged from
/// <see cref="IBuiltInSpeechCatalog.AllRecipes"/>; tenant rows live in <c>AnkVoiceRecipes</c>.
/// Validation enforces that referenced provider ids resolve to providers of the right type.
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Service Surface".
/// </para>
/// </summary>
internal sealed class VoiceRecipeLibraryService : IVoiceRecipeLibraryService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly VoiceDbContext _db;
    private readonly IBuiltInSpeechCatalog _builtIns;
    private readonly ISpeechProviderLibraryService _providers;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly IClock _clock;

    public VoiceRecipeLibraryService(
        VoiceDbContext db,
        IBuiltInSpeechCatalog builtIns,
        ISpeechProviderLibraryService providers,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        IClock clock)
    {
        _db = db;
        _builtIns = builtIns;
        _providers = providers;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    public async Task<IReadOnlyList<VoiceRecipe>> ListAsync(
        VoiceRecipeKind? kind = null,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.VoiceRecipes.AsNoTracking();
        if (kind is { } k)
        {
            query = query.Where(r => r.Kind == k);
        }
        if (!includeDisabled)
        {
            query = query.Where(r => r.Status == VoiceRecipeStatus.Active);
        }

        var rows = await query
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var builtIns = _builtIns.AllRecipes.AsEnumerable();
        if (kind is { } k2)
        {
            builtIns = builtIns.Where(r => r.Kind == k2);
        }

        return rows.Select(ToDomain)
            .Concat(builtIns)
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.IsBuiltIn ? 0 : 1)
            .ThenBy(r => r.DisplayName)
            .ToList();
    }

    public async Task<VoiceRecipe?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal))
        {
            return _builtIns.FindRecipe(id);
        }
        if (!Guid.TryParse(id, out var guid)) return null;

        var row = await _db.VoiceRecipes
            .AsNoTracking()
            .Where(r => r.Id == guid && r.Status != VoiceRecipeStatus.SoftDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    public async Task<VoiceRecipe> CreateAsync(
        CreateVoiceRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(request.DisplayName);
        await ValidateBodyAsync(request.Kind, request.Chained, request.Composite, cancellationToken);

        var entity = new VoiceRecipeEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.GetCurrentTenantId(),
            DisplayName = request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Kind = request.Kind,
            Status = VoiceRecipeStatus.Active,
            Version = 1,
            PreviousVersionsJson = "[]",
        };
        ApplyBody(entity, request.Chained, request.Composite);

        _db.VoiceRecipes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(entity);
    }

    public async Task<VoiceRecipe> UpdateAsync(
        Guid id,
        UpdateVoiceRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadTenantOwnedAsync(id, cancellationToken).ConfigureAwait(false);
        ValidateDisplayName(request.DisplayName);
        await ValidateBodyAsync(row.Kind, request.Chained, request.Composite, cancellationToken);

        AppendHistorySnapshot(row, VoiceRecipeHistoryAction.Updated);

        row.DisplayName = request.DisplayName.Trim();
        row.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ApplyBody(row, request.Chained, request.Composite);
        row.Version += 1;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(row);
    }

    public async Task<VoiceRecipe> CloneBuiltInAsync(
        string builtInId,
        string? newDisplayName,
        CancellationToken cancellationToken = default)
    {
        var built = _builtIns.FindRecipe(builtInId)
            ?? throw new SpeechLibraryValidationException(
                $"Built-in recipe '{builtInId}' does not exist.",
                fieldName: nameof(builtInId));

        var name = string.IsNullOrWhiteSpace(newDisplayName)
            ? $"{built.DisplayName} (copy)"
            : newDisplayName.Trim();

        return await CreateAsync(new CreateVoiceRecipeRequest(
            DisplayName: name,
            Description: built.Description,
            Kind: built.Kind,
            Chained: built.Chained,
            Composite: built.Composite),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<VoiceRecipe> SetStatusAsync(
        Guid id,
        VoiceRecipeStatus status,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadTenantOwnedAsync(id, cancellationToken).ConfigureAwait(false);
        if (row.Status == status) return ToDomain(row);

        // Phase F guard: refuse to disable / soft-delete the currently-active Voice Mode recipe.
        // The admin must pick a different one (or turn Voice Mode off) before disabling, so the
        // WSS pipeline never tries to bind to a recipe row that's no longer Active.
        if (row.Status == VoiceRecipeStatus.Active && status != VoiceRecipeStatus.Active)
        {
            await EnsureNotActiveInVoiceModeAsync(row.Id, row.TenantId, cancellationToken)
                .ConfigureAwait(false);
        }

        AppendHistorySnapshot(row, VoiceRecipeHistoryAction.StatusChanged);
        row.Status = status;
        row.Version += 1;
        if (status == VoiceRecipeStatus.SoftDeleted)
        {
            row.IsDeleted = true;
            row.DeletedAt = _clock.UtcNow;
            row.DeletedBy = _user.GetCurrentUserId();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(row);
    }

    public async Task<IReadOnlyList<VoiceRecipeHistoryEntry>> GetHistoryAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal))
        {
            return Array.Empty<VoiceRecipeHistoryEntry>();
        }
        if (!Guid.TryParse(id, out var guid))
        {
            return Array.Empty<VoiceRecipeHistoryEntry>();
        }

        var json = await _db.VoiceRecipes
            .AsNoTracking()
            .Where(r => r.Id == guid)
            .Select(r => r.PreviousVersionsJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return json is null ? Array.Empty<VoiceRecipeHistoryEntry>() : DeserialiseHistory(json);
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private async Task<VoiceRecipeEntity> LoadTenantOwnedAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.VoiceRecipes
            .Where(r => r.Id == id && r.Status != VoiceRecipeStatus.SoftDeleted)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            throw new SpeechLibraryValidationException(
                $"Voice recipe '{id}' does not exist or has been deleted.",
                fieldName: nameof(id));
        }
        return row;
    }

    /// <summary>
    /// Disable / soft-delete guard. Throws when the recipe is currently the active Voice Mode
    /// recipe; the admin must switch first. We surface this as 422 (validation) rather than 409
    /// because there's only one possible blocker (no list of dependents to enumerate) — the
    /// remediation message tells the whole story.
    /// </summary>
    private async Task EnsureNotActiveInVoiceModeAsync(Guid recipeId, Guid tenantId, CancellationToken ct)
    {
        var idStr = recipeId.ToString("N");
        var activeRecipeId = await _db.VoiceModeSettings
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ActiveRecipeId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (string.Equals(activeRecipeId, idStr, StringComparison.Ordinal))
        {
            throw new SpeechLibraryValidationException(
                "Cannot disable: this recipe is currently the active Voice Mode recipe. "
                    + "Switch Voice Mode to a different recipe (or turn it off) before disabling.",
                fieldName: nameof(recipeId));
        }
    }

    private async Task ValidateBodyAsync(
        VoiceRecipeKind kind,
        ChainedRecipeBody? chained,
        CompositeRecipeBody? composite,
        CancellationToken ct)
    {
        if (kind == VoiceRecipeKind.Chained)
        {
            if (chained is null)
            {
                throw new SpeechLibraryValidationException("Chained recipes must include a chained body.", fieldName: nameof(chained));
            }
            if (composite is not null)
            {
                throw new SpeechLibraryValidationException("Chained recipes must NOT include a composite body.", fieldName: nameof(composite));
            }
            await ValidateProviderRefAsync(chained.SttProviderId, SpeechProviderType.Stt, nameof(chained.SttProviderId), ct);
            var ttsProvider = await ValidateProviderRefAsync(chained.TtsProviderId, SpeechProviderType.Tts, nameof(chained.TtsProviderId), ct);

            // Voice id moved off the provider config (post-spec-024 refactor); recipes now own
            // it. Required because there's no sensible vendor-level default once you have
            // per-recipe voices.
            if (string.IsNullOrWhiteSpace(chained.TtsVoiceId))
            {
                throw new SpeechLibraryValidationException(
                    "Chained recipes must specify a TTS voice id.",
                    fieldName: nameof(chained.TtsVoiceId));
            }
            // Catch the most common foot-gun: changing the TTS provider in the recipe
            // editor without updating the voice picker. OpenAI's TTS expects a built-in
            // voice name; Mistral / ElevenLabs expect a GUID-formatted id; Azure expects
            // `locale-VoiceName`. A GUID landing on OpenAI 400s with no useful client
            // signal — better to reject at save time with a clear field error.
            ValidateVoiceIdShapeForVendor(ttsProvider.Vendor, chained.TtsVoiceId, nameof(chained.TtsVoiceId));
        }
        else if (kind == VoiceRecipeKind.Composite)
        {
            if (composite is null)
            {
                throw new SpeechLibraryValidationException("Composite recipes must include a composite body.", fieldName: nameof(composite));
            }
            if (chained is not null)
            {
                throw new SpeechLibraryValidationException("Composite recipes must NOT include a chained body.", fieldName: nameof(chained));
            }
            await ValidateProviderRefAsync(composite.CompositeProviderId, SpeechProviderType.Composite, nameof(composite.CompositeProviderId), ct);

            if (string.IsNullOrWhiteSpace(composite.Voice))
            {
                throw new SpeechLibraryValidationException(
                    "Composite recipes must specify a voice.",
                    fieldName: nameof(composite.Voice));
            }
        }
    }

    private async Task<SpeechProvider> ValidateProviderRefAsync(
        string providerId,
        SpeechProviderType expectedType,
        string fieldName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new SpeechLibraryValidationException($"{fieldName} is required.", fieldName);
        }
        var provider = await _providers.GetAsync(providerId, ct);
        if (provider is null)
        {
            throw new SpeechLibraryValidationException(
                $"Referenced provider '{providerId}' does not exist or has been deleted.",
                fieldName);
        }
        if (provider.Type != expectedType)
        {
            throw new SpeechLibraryValidationException(
                $"Referenced provider '{providerId}' is type {provider.Type}, expected {expectedType}.",
                fieldName);
        }
        return provider;
    }

    // OpenAI's TTS voices are the six built-in names — no other shape is accepted.
    // Kept here rather than fetched from the OpenAI API per save: the list is small,
    // changes rarely, and we want a fast offline validation.
    private static readonly HashSet<string> OpenAiTtsVoiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "alloy", "echo", "fable", "onyx", "nova", "shimmer", "ash", "coral", "sage",
    };

    /// <summary>
    /// ElevenLabs voice ids: exactly 20 alphanumeric characters. The library UI
    /// returns ids like <c>21m00Tcm4TlvDq8ikWAM</c>, <c>EXAVITQu4vr4xnSDxMaL</c>.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ElevenLabsVoiceIdRegex =
        new(@"^[A-Za-z0-9]{20}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Reject voice ids that obviously don't belong to the recipe's TTS vendor — the
    /// classic case is keeping a Mistral GUID after swapping the TTS provider to
    /// OpenAI in the recipe editor (UI doesn't currently reset the voice picker on
    /// provider change). This is a shape check, not an authoritative voice-exists
    /// check; the engine itself will surface 4xx if the voice id is unknown to the
    /// vendor's catalog. Goal is to catch the obvious mismatch up-front with a
    /// useful field-level error.
    /// </summary>
    private static void ValidateVoiceIdShapeForVendor(string vendor, string voiceId, string fieldName)
    {
        var normalisedVendor = (vendor ?? string.Empty).Trim().ToLowerInvariant();
        var trimmedVoice = voiceId.Trim();
        var isGuidShaped = Guid.TryParse(trimmedVoice, out _);

        switch (normalisedVendor)
        {
            case "openai":
                if (isGuidShaped)
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' looks like a Mistral / ElevenLabs voice id. "
                        + "OpenAI TTS expects a built-in voice name (alloy, echo, fable, onyx, nova, shimmer).",
                        fieldName);
                }
                if (!OpenAiTtsVoiceNames.Contains(trimmedVoice))
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' isn't a recognised OpenAI TTS voice. "
                        + "Pick one of: " + string.Join(", ", OpenAiTtsVoiceNames) + ".",
                        fieldName);
                }
                break;

            case "mistral":
                // Mistral's /v1/audio/voices catalog returns GUIDs.
                if (!isGuidShaped)
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' doesn't look like a Mistral voice id "
                        + "(expected a GUID such as 90b8805d-8e89-4ecc-adc7-a40e62cb1710).",
                        fieldName);
                }
                break;

            case "elevenlabs":
                // ElevenLabs voice ids from the Voice Library are 20-character
                // alphanumeric strings (e.g. `21m00Tcm4TlvDq8ikWAM`). They are
                // NOT GUIDs — rejecting GUIDs explicitly guards against the
                // copy-paste-from-Mistral mistake, then the regex catches typos.
                if (isGuidShaped)
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' is a GUID; ElevenLabs voice ids are "
                        + "20-character alphanumeric strings (e.g. 21m00Tcm4TlvDq8ikWAM). "
                        + "Copy the voice id from elevenlabs.io's Voice Library.",
                        fieldName);
                }
                if (!ElevenLabsVoiceIdRegex.IsMatch(trimmedVoice))
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' doesn't look like an ElevenLabs voice id "
                        + "(expected 20 alphanumeric characters, e.g. 21m00Tcm4TlvDq8ikWAM).",
                        fieldName);
                }
                break;

            case "azure":
                // Azure neural voices follow `<locale>-<VoiceName>Neural` (e.g.
                // en-US-JennyNeural). The `Neural` suffix is conventional; other
                // shapes exist (e.g. classic voices) but the neural variant is the
                // default and shipping recipes consistently use it.
                if (isGuidShaped || !trimmedVoice.Contains('-'))
                {
                    throw new SpeechLibraryValidationException(
                        $"Voice '{trimmedVoice}' doesn't look like an Azure Speech voice "
                        + "(expected something like 'en-US-JennyNeural').",
                        fieldName);
                }
                break;

            // Unknown vendors (e.g. openai-realtime composite) — no shape check; the
            // engine will surface its own errors at connect time.
        }
    }

    private static void ApplyBody(
        VoiceRecipeEntity entity,
        ChainedRecipeBody? chained,
        CompositeRecipeBody? composite)
    {
        if (chained is not null)
        {
            entity.ChainedSttProviderId = chained.SttProviderId;
            entity.ChainedTtsProviderId = chained.TtsProviderId;
            entity.ChainedTtsVoiceId = chained.TtsVoiceId;
            entity.ChainedTtsModelId = chained.TtsModelId;
            entity.ChainedSttModel = chained.SttModel;
            entity.ChainedSttLanguage = chained.SttLanguage;
            entity.ChainedPinnedAgentId = chained.PinnedAgentId;
            entity.ChainedVad = chained.Vad;
            entity.ChainedVadStopMs = chained.VadStopMs;
            entity.ChainedTranscriptionFilter = chained.TranscriptionFilter;
            entity.ChainedSentenceAggregator = chained.SentenceAggregator;
            entity.CompositeProviderId = null;
            entity.CompositeVoice = null;
            entity.CompositeModel = null;
            entity.CompositeInstructionsAddendum = null;
            entity.CompositePinnedAgentId = null;
        }
        else if (composite is not null)
        {
            entity.CompositeProviderId = composite.CompositeProviderId;
            entity.CompositeVoice = composite.Voice;
            entity.CompositeModel = composite.Model;
            entity.CompositeInstructionsAddendum = composite.InstructionsAddendum;
            entity.CompositePinnedAgentId = composite.PinnedAgentId;
            entity.ChainedSttProviderId = null;
            entity.ChainedTtsProviderId = null;
            entity.ChainedTtsVoiceId = null;
            entity.ChainedTtsModelId = null;
            entity.ChainedSttModel = null;
            entity.ChainedSttLanguage = null;
            entity.ChainedPinnedAgentId = null;
            entity.ChainedVad = null;
            entity.ChainedVadStopMs = null;
            entity.ChainedTranscriptionFilter = null;
            entity.ChainedSentenceAggregator = null;
        }
    }

    private void AppendHistorySnapshot(VoiceRecipeEntity row, VoiceRecipeHistoryAction action)
    {
        var existing = DeserialiseHistory(row.PreviousVersionsJson).ToList();

        var snapshot = new VoiceRecipeHistoryEntry(
            Version: row.Version,
            Action: action,
            SnapshotDisplayName: row.DisplayName,
            SnapshotDescription: row.Description,
            SnapshotStatus: row.Status,
            SnapshotChained: row.Kind == VoiceRecipeKind.Chained
                ? new ChainedRecipeBody(
                    SttProviderId: row.ChainedSttProviderId ?? string.Empty,
                    TtsProviderId: row.ChainedTtsProviderId ?? string.Empty,
                    TtsVoiceId: row.ChainedTtsVoiceId ?? string.Empty,
                    TtsModelId: row.ChainedTtsModelId,
                    SttModel: row.ChainedSttModel,
                    SttLanguage: row.ChainedSttLanguage,
                    PinnedAgentId: row.ChainedPinnedAgentId,
                    Vad: row.ChainedVad ?? "energy",
                    VadStopMs: row.ChainedVadStopMs,
                    TranscriptionFilter: row.ChainedTranscriptionFilter ?? true,
                    SentenceAggregator: row.ChainedSentenceAggregator ?? true)
                : null,
            SnapshotComposite: row.Kind == VoiceRecipeKind.Composite
                ? new CompositeRecipeBody(
                    CompositeProviderId: row.CompositeProviderId ?? string.Empty,
                    Voice: row.CompositeVoice ?? string.Empty,
                    Model: row.CompositeModel,
                    InstructionsAddendum: row.CompositeInstructionsAddendum,
                    PinnedAgentId: row.CompositePinnedAgentId)
                : null,
            At: _clock.UtcNow,
            ByUserId: _user.GetCurrentUserId());

        existing.Insert(0, snapshot);
        if (existing.Count > SpeechLibraryConstants.HistoryRetentionPerEntity)
        {
            existing = existing.Take(SpeechLibraryConstants.HistoryRetentionPerEntity).ToList();
        }
        row.PreviousVersionsJson = JsonSerializer.Serialize(existing, JsonOpts);
    }

    private static IReadOnlyList<VoiceRecipeHistoryEntry> DeserialiseHistory(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return Array.Empty<VoiceRecipeHistoryEntry>();
        return JsonSerializer.Deserialize<List<VoiceRecipeHistoryEntry>>(json, JsonOpts)
            ?? new List<VoiceRecipeHistoryEntry>();
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new SpeechLibraryValidationException("Display name is required.", nameof(displayName));
        if (displayName.Length > 200)
            throw new SpeechLibraryValidationException("Display name must be 200 characters or fewer.", nameof(displayName));
    }

    private static VoiceRecipe ToDomain(VoiceRecipeEntity row)
        => new(
            Id: row.Id.ToString("N"),
            DisplayName: row.DisplayName,
            Description: row.Description,
            Kind: row.Kind,
            Chained: row.Kind == VoiceRecipeKind.Chained
                ? new ChainedRecipeBody(
                    SttProviderId: row.ChainedSttProviderId ?? string.Empty,
                    TtsProviderId: row.ChainedTtsProviderId ?? string.Empty,
                    TtsVoiceId: row.ChainedTtsVoiceId ?? string.Empty,
                    TtsModelId: row.ChainedTtsModelId,
                    SttModel: row.ChainedSttModel,
                    SttLanguage: row.ChainedSttLanguage,
                    PinnedAgentId: row.ChainedPinnedAgentId,
                    Vad: row.ChainedVad ?? "energy",
                    VadStopMs: row.ChainedVadStopMs,
                    TranscriptionFilter: row.ChainedTranscriptionFilter ?? true,
                    SentenceAggregator: row.ChainedSentenceAggregator ?? true)
                : null,
            Composite: row.Kind == VoiceRecipeKind.Composite
                ? new CompositeRecipeBody(
                    CompositeProviderId: row.CompositeProviderId ?? string.Empty,
                    Voice: row.CompositeVoice ?? string.Empty,
                    Model: row.CompositeModel,
                    InstructionsAddendum: row.CompositeInstructionsAddendum,
                    PinnedAgentId: row.CompositePinnedAgentId)
                : null,
            IsBuiltIn: false,
            Status: row.Status,
            Version: row.Version,
            CreatedAt: new DateTimeOffset(row.CreatedAt, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(row.UpdatedAt ?? row.CreatedAt, TimeSpan.Zero),
            CreatedByUserId: row.CreatedBy,
            LastUpdatedByUserId: row.UpdatedBy);
}
