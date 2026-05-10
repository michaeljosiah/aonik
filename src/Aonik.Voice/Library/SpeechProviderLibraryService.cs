using System.Text.Json;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Entities;
using Aonik.Voice.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Library;

/// <summary>
/// Per-tenant CRUD over the speech provider library. Built-in archetypes (from
/// <see cref="IBuiltInSpeechCatalog"/>) are merged into list responses; tenant-owned rows live
/// in <c>AnkSpeechProviders</c>. Validation, version bumping, history snapshot writing, AND
/// API-key encryption all happen here so the endpoint layer is a thin pass-through.
///
/// <para>
/// One-row-per-vendor enforcement is also done here — the unique index in EF backs it at the
/// DB level, but the service catches duplicates first and returns a clear error rather than
/// surfacing a SqlException.
/// </para>
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Service Surface".
/// </para>
/// </summary>
internal sealed class SpeechProviderLibraryService : ISpeechProviderLibraryService
{
    private const string ApiKeyProtectionPurpose = "Aonik.Voice.SpeechProvider.ApiKey";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly VoiceDbContext _db;
    private readonly IBuiltInSpeechCatalog _builtIns;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly IClock _clock;
    private readonly IDataProtector _protector;
    private readonly ISpeechCredentialCacheInvalidator _credentialCache;

    public SpeechProviderLibraryService(
        VoiceDbContext db,
        IBuiltInSpeechCatalog builtIns,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        IClock clock,
        IDataProtectionProvider dataProtectionProvider,
        ISpeechCredentialCacheInvalidator credentialCache)
    {
        _db = db;
        _builtIns = builtIns;
        _tenant = tenant;
        _user = user;
        _clock = clock;
        _protector = dataProtectionProvider.CreateProtector(ApiKeyProtectionPurpose);
        _credentialCache = credentialCache;
    }

    // ── List ────────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpeechProvider>> ListAsync(
        SpeechProviderType? type = null,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SpeechProviders.AsNoTracking();

        if (type is { } t)
        {
            query = query.Where(p => p.Type == t);
        }
        if (!includeDisabled)
        {
            query = query.Where(p => p.Status == SpeechProviderStatus.Active);
        }

        var rows = await query
            .OrderBy(p => p.Type)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Merge built-ins. The catalog is empty today (post-spec-024 we ditched archetypes in
        // favour of the create-your-own flow) but the merge stays in case we re-introduce
        // archetypes later.
        var builtIns = _builtIns.AllProviders.AsEnumerable();
        if (type is { } t2)
        {
            builtIns = builtIns.Where(p => p.Type == t2);
        }

        return rows.Select(ToDomain)
            .Concat(builtIns)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.IsBuiltIn ? 0 : 1) // built-ins first within each type
            .ThenBy(p => p.DisplayName)
            .ToList();
    }

    // ── Get ─────────────────────────────────────────────────────────────────────────────

    public async Task<SpeechProvider?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal))
        {
            return _builtIns.FindProvider(id);
        }

        if (!Guid.TryParse(id, out var guid))
        {
            return null;
        }

        var row = await _db.SpeechProviders
            .AsNoTracking()
            .Where(p => p.Id == guid && p.Status != SpeechProviderStatus.SoftDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    // ── Create ──────────────────────────────────────────────────────────────────────────

    public async Task<SpeechProvider> CreateAsync(
        CreateSpeechProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfigShape(request.Type, request.Vendor, request.Config);
        ValidateDisplayName(request.DisplayName);

        var tenantId = _tenant.GetCurrentTenantId();
        var normalisedVendor = NormaliseVendor(request.Vendor);

        // Enforce one-row-per-(tenant, vendor, type). The DB-level unique index backs this;
        // we pre-check so the failure is a clean 422 with a friendly message instead of a SQL
        // unique-violation surfacing through the endpoint. Different types of the same vendor
        // (e.g. OpenAI STT + OpenAI TTS) are allowed and share a credential via the unified
        // resolver.
        var alreadyExists = await _db.SpeechProviders
            .AsNoTracking()
            .AnyAsync(
                p => p.TenantId == tenantId
                    && p.Vendor == normalisedVendor
                    && p.Type == request.Type
                    && !p.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyExists)
        {
            throw new SpeechLibraryValidationException(
                $"A {request.Type} provider for vendor '{normalisedVendor}' already exists in this tenant's library. "
                + "Edit that provider instead — only one provider per vendor + type is supported.",
                fieldName: nameof(request.Vendor));
        }

        var entity = new SpeechProviderEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = request.DisplayName.Trim(),
            Type = request.Type,
            Vendor = normalisedVendor,
            ConfigJson = SerializeConfig(request.Config),
            EncryptedApiKey = ProtectIfPresent(request.ApiKey),
            Status = SpeechProviderStatus.Active,
            Version = 1,
            PreviousVersionsJson = "[]",
        };

        _db.SpeechProviders.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Cache nuke so the next credential resolve sees the new key without waiting for TTL.
        await _credentialCache.InvalidateAsync(entity.Vendor, entity.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return ToDomain(entity);
    }

    // ── Update ──────────────────────────────────────────────────────────────────────────

    public async Task<SpeechProvider> UpdateAsync(
        Guid id,
        UpdateSpeechProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadTenantOwnedAsync(id, cancellationToken).ConfigureAwait(false);
        ValidateConfigShape(row.Type, row.Vendor, request.Config);
        ValidateDisplayName(request.DisplayName);

        AppendHistorySnapshot(row, SpeechProviderHistoryAction.Updated);

        row.DisplayName = request.DisplayName.Trim();
        row.ConfigJson = SerializeConfig(request.Config);

        // Tri-state ApiKey: null = leave alone, "" = clear, non-empty = encrypt + replace.
        var apiKeyChanged = request.ApiKey is not null;
        if (apiKeyChanged)
        {
            row.EncryptedApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
                ? null
                : _protector.Protect(request.ApiKey!.Trim());
        }

        row.Version += 1;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (apiKeyChanged)
        {
            await _credentialCache.InvalidateAsync(row.Vendor, row.TenantId, cancellationToken)
                .ConfigureAwait(false);
        }

        return ToDomain(row);
    }

    // ── Clone built-in ──────────────────────────────────────────────────────────────────

    public async Task<SpeechProvider> CloneBuiltInAsync(
        string builtInId,
        string? newDisplayName,
        CancellationToken cancellationToken = default)
    {
        // Built-in catalog is empty post-spec-024 but the interface contract stays. The clone
        // path is essentially dead but preserved so callers don't break; once the frontend
        // drops its clone affordances entirely we can yank this method.
        var built = _builtIns.FindProvider(builtInId)
            ?? throw new SpeechLibraryValidationException(
                $"Built-in provider '{builtInId}' does not exist.",
                fieldName: nameof(builtInId));

        var name = string.IsNullOrWhiteSpace(newDisplayName)
            ? $"{built.DisplayName} (copy)"
            : newDisplayName.Trim();

        return await CreateAsync(new CreateSpeechProviderRequest(
            DisplayName: name,
            Type: built.Type,
            Vendor: built.Vendor,
            Config: built.Config),
            cancellationToken).ConfigureAwait(false);
    }

    // ── Status ──────────────────────────────────────────────────────────────────────────

    public async Task<SpeechProvider> SetStatusAsync(
        Guid id,
        SpeechProviderStatus status,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadTenantOwnedAsync(id, cancellationToken).ConfigureAwait(false);

        if (row.Status == status)
        {
            return ToDomain(row);
        }

        // TODO: Phase D follow-up — gate disable/soft-delete on active recipe usage so admins
        // can't accidentally disable a provider their voice mode recipe depends on. For now
        // the runtime fails with a clear "provider not active" error at connection time, which
        // is recoverable.

        AppendHistorySnapshot(row, SpeechProviderHistoryAction.StatusChanged);
        row.Status = status;
        row.Version += 1;

        if (status == SpeechProviderStatus.SoftDeleted)
        {
            row.IsDeleted = true;
            row.DeletedAt = _clock.UtcNow;
            row.DeletedBy = _user.GetCurrentUserId();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(row);
    }

    // ── History ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpeechProviderHistoryEntry>> GetHistoryAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal))
        {
            return Array.Empty<SpeechProviderHistoryEntry>();
        }

        if (!Guid.TryParse(id, out var guid))
        {
            return Array.Empty<SpeechProviderHistoryEntry>();
        }

        var row = await _db.SpeechProviders
            .AsNoTracking()
            .Where(p => p.Id == guid)
            .Select(p => p.PreviousVersionsJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Array.Empty<SpeechProviderHistoryEntry>();
        }

        return DeserialiseHistory(row);
    }

    // ── Usage ───────────────────────────────────────────────────────────────────────────

    public async Task<SpeechProviderUsage> GetUsageAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.VoiceRecipes
            .AsNoTracking()
            .Where(r => r.Status != VoiceRecipeStatus.SoftDeleted
                && (r.ChainedSttProviderId == id
                    || r.ChainedTtsProviderId == id
                    || r.CompositeProviderId == id))
            .Select(r => new SpeechProviderUsageRecipeRef(
                r.Id.ToString("N"),
                r.DisplayName,
                false))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var builtInRefs = _builtIns.AllRecipes
            .Where(r =>
                (r.Chained?.SttProviderId == id) ||
                (r.Chained?.TtsProviderId == id) ||
                (r.Composite?.CompositeProviderId == id))
            .Select(r => new SpeechProviderUsageRecipeRef(r.Id, r.DisplayName, false))
            .ToList();

        return new SpeechProviderUsage(rows.Concat(builtInRefs).ToList());
    }

    // ── Internals ───────────────────────────────────────────────────────────────────────

    private async Task<SpeechProviderEntity> LoadTenantOwnedAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.SpeechProviders
            .Where(p => p.Id == id && p.Status != SpeechProviderStatus.SoftDeleted)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new SpeechLibraryValidationException(
                $"Speech provider '{id}' does not exist or has been deleted.",
                fieldName: nameof(id));
        }

        return row;
    }

    private void AppendHistorySnapshot(SpeechProviderEntity row, SpeechProviderHistoryAction action)
    {
        var existing = DeserialiseHistory(row.PreviousVersionsJson).ToList();

        var snapshot = new SpeechProviderHistoryEntry(
            Version: row.Version,
            Action: action,
            SnapshotDisplayName: row.DisplayName,
            SnapshotStatus: row.Status,
            SnapshotConfig: DeserializeConfig(row.ConfigJson),
            At: _clock.UtcNow,
            ByUserId: _user.GetCurrentUserId());

        existing.Insert(0, snapshot);

        if (existing.Count > SpeechLibraryConstants.HistoryRetentionPerEntity)
        {
            existing = existing.Take(SpeechLibraryConstants.HistoryRetentionPerEntity).ToList();
        }

        row.PreviousVersionsJson = JsonSerializer.Serialize(existing, JsonOpts);
    }

    private static IReadOnlyList<SpeechProviderHistoryEntry> DeserialiseHistory(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return Array.Empty<SpeechProviderHistoryEntry>();
        }
        return JsonSerializer.Deserialize<List<SpeechProviderHistoryEntry>>(json, JsonOpts)
            ?? new List<SpeechProviderHistoryEntry>();
    }

    private static string SerializeConfig(SpeechProviderConfig config)
        => JsonSerializer.Serialize(config, JsonOpts);

    private static SpeechProviderConfig DeserializeConfig(string json)
        => JsonSerializer.Deserialize<SpeechProviderConfig>(json, JsonOpts)
            ?? throw new InvalidDataException("Speech provider config JSON deserialised to null.");

    private string? ProtectIfPresent(string? plaintext)
        => string.IsNullOrWhiteSpace(plaintext) ? null : _protector.Protect(plaintext.Trim());

    private static void ValidateConfigShape(SpeechProviderType type, string vendor, SpeechProviderConfig config)
    {
        var v = vendor.ToLowerInvariant();
        var ok = type switch
        {
            SpeechProviderType.Stt => v switch
            {
                "openai" or "openai-whisper" => config is OpenAIWhisperConfig,
                "azure" => config is AzureSttConfig,
                _ => false,
            },
            SpeechProviderType.Tts => v switch
            {
                "openai" => config is OpenAITtsConfig,
                "azure" => config is AzureTtsConfig,
                "elevenlabs" => config is ElevenLabsTtsConfig,
                "mistral" => config is MistralTtsConfig,
                _ => false,
            },
            SpeechProviderType.Composite => v switch
            {
                "openai-realtime" => config is OpenAIRealtimeCompositeConfig,
                "azure-voice-live" => config is AzureVoiceLiveCompositeConfig,
                _ => false,
            },
            _ => false,
        };

        if (!ok)
        {
            throw new SpeechLibraryValidationException(
                $"Config payload type does not match (Type={type}, Vendor={vendor}).",
                fieldName: nameof(config));
        }
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new SpeechLibraryValidationException("Display name is required.", fieldName: nameof(displayName));
        }
        if (displayName.Length > 200)
        {
            throw new SpeechLibraryValidationException("Display name must be 200 characters or fewer.", fieldName: nameof(displayName));
        }
    }

    private static string NormaliseVendor(string vendor)
        => (vendor ?? string.Empty).Trim().ToLowerInvariant();

    private static SpeechProvider ToDomain(SpeechProviderEntity row)
        => new(
            Id: row.Id.ToString("N"),
            DisplayName: row.DisplayName,
            Type: row.Type,
            Vendor: row.Vendor,
            Config: DeserializeConfig(row.ConfigJson),
            Status: row.Status,
            HasApiKey: !string.IsNullOrWhiteSpace(row.EncryptedApiKey),
            IsBuiltIn: false,
            Version: row.Version,
            CreatedAt: new DateTimeOffset(row.CreatedAt, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(row.UpdatedAt ?? row.CreatedAt, TimeSpan.Zero),
            CreatedByUserId: row.CreatedBy,
            LastUpdatedByUserId: row.UpdatedBy);
}
