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

        // Phase C will plug the active-recipe check in here so we can refuse to soft-delete the
        // currently-active recipe. Phase B ships the gate as a no-op.

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
            await ValidateProviderRefAsync(chained.TtsProviderId, SpeechProviderType.Tts, nameof(chained.TtsProviderId), ct);
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
        }
    }

    private async Task ValidateProviderRefAsync(
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
            entity.ChainedPinnedAgentId = chained.PinnedAgentId;
            entity.ChainedVad = chained.Vad;
            entity.ChainedVadStopMs = chained.VadStopMs;
            entity.ChainedTranscriptionFilter = chained.TranscriptionFilter;
            entity.ChainedSentenceAggregator = chained.SentenceAggregator;
            entity.CompositeProviderId = null;
            entity.CompositePinnedAgentId = null;
        }
        else if (composite is not null)
        {
            entity.CompositeProviderId = composite.CompositeProviderId;
            entity.CompositePinnedAgentId = composite.PinnedAgentId;
            entity.ChainedSttProviderId = null;
            entity.ChainedTtsProviderId = null;
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
                    PinnedAgentId: row.ChainedPinnedAgentId,
                    Vad: row.ChainedVad ?? "energy",
                    VadStopMs: row.ChainedVadStopMs,
                    TranscriptionFilter: row.ChainedTranscriptionFilter ?? true,
                    SentenceAggregator: row.ChainedSentenceAggregator ?? true)
                : null,
            SnapshotComposite: row.Kind == VoiceRecipeKind.Composite
                ? new CompositeRecipeBody(
                    CompositeProviderId: row.CompositeProviderId ?? string.Empty,
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
                    PinnedAgentId: row.ChainedPinnedAgentId,
                    Vad: row.ChainedVad ?? "energy",
                    VadStopMs: row.ChainedVadStopMs,
                    TranscriptionFilter: row.ChainedTranscriptionFilter ?? true,
                    SentenceAggregator: row.ChainedSentenceAggregator ?? true)
                : null,
            Composite: row.Kind == VoiceRecipeKind.Composite
                ? new CompositeRecipeBody(
                    CompositeProviderId: row.CompositeProviderId ?? string.Empty,
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
