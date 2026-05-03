using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Services;

/// <summary>
/// Cache wrapper for synthesized TTS audio bytes. Scoped narrowly to
/// "static / pre-warmed phrases" in v1: <see cref="IsAllowlisted"/>
/// gates write-through, and the streaming service only consults the
/// cache for entries whose normalized text is allowlisted.
/// </summary>
/// <remarks>
/// <para>
/// User-generated personal-finance speech is <em>not</em> cached in v1
/// — privacy reviewed. The allowlist is intentionally small and lives
/// in <see cref="TtsCacheAllowlist"/>.
/// </para>
/// <para>
/// Keys hash the normalized text via SHA-256 so the raw transcript
/// never appears in cache keys. Each entry carries enough metadata
/// (provider, voice, model, format, locale, and original
/// <see cref="TtsCacheEntry.OriginalAiRunId"/>) for an audit trail to
/// survive cache hits.
/// </para>
/// </remarks>
internal interface ITtsCache
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="normalizedText"/> matches
    /// one of the pre-allowlisted stock phrases. Non-allowlisted text is
    /// neither read from nor written to the cache.
    /// </summary>
    bool IsAllowlisted(string normalizedText);

    /// <summary>
    /// Look up a cache entry. Returns <c>null</c> if the key isn't
    /// allowlisted, isn't present, or fails to deserialize.
    /// </summary>
    ValueTask<TtsCacheEntry?> TryGetAsync(TtsCacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist <paramref name="entry"/> for <paramref name="key"/>. Silently
    /// no-ops when the key isn't allowlisted, so callers can invoke
    /// unconditionally on the synthesis path.
    /// </summary>
    ValueTask SetAsync(TtsCacheKey key, TtsCacheEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composite cache key. Text is SHA-256 hashed before being mixed in so
/// the raw user/agent transcript never lands in the cache layer.
/// </summary>
internal readonly record struct TtsCacheKey(
    string TextHash,
    Guid TenantId,
    string Provider,
    string VoiceId,
    string? ModelId,
    string? Format,
    string? Locale)
{
    public string Serialize() =>
        $"tts:{TenantId:N}:{Provider}:{VoiceId}:{ModelId ?? "_"}:{Format ?? "_"}:{Locale ?? "_"}:{TextHash}";
}

internal sealed record TtsCacheEntry(
    byte[] Audio,
    string ContentType,
    string Provider,
    string VoiceId,
    string? ModelId,
    Guid OriginalAiRunId,
    DateTimeOffset CreatedAtUtc);
