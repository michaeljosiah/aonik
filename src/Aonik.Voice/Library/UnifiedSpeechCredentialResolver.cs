using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Voice.Library;

/// <summary>
/// Single resolver for both voice-pipeline credentials and chat-TTS credentials. Reads the
/// tenant override directly from the <c>SpeechProvider</c> row (if one exists for the requested
/// vendor) and falls back to the host-default + configuration-fallback chain owned by
/// <see cref="IVoiceProviderCredentialSettingsService"/>.
///
/// <para>
/// This replaces the dual <c>VoiceProviderCredentialSettingsService</c> /
/// <c>TextToSpeechCredentialSettingsService</c> tenant-override storage with a single source of
/// truth — the provider library row. Host defaults stay in the existing service so admin-only
/// org-level keys (shared across all tenants) keep working.
/// </para>
///
/// <para>
/// Vendor name normalisation: callers pass vendor strings in mixed case (e.g. <c>"OpenAI"</c>
/// from the legacy voice factory, <c>"ElevenLabs"</c> from the chat-TTS service). We
/// lower-case before lookup so they match the normalised <c>SpeechProvider.Vendor</c>
/// shortcode, and pass the original string back to the host-default fallback unchanged.
/// </para>
/// </summary>
internal sealed class UnifiedSpeechCredentialResolver
    : IVoiceProviderCredentialResolver, ITextToSpeechCredentialResolver, ISpeechCredentialCacheInvalidator
{
    private const string ApiKeyProtectionPurpose = "Aonik.Voice.SpeechProvider.ApiKey";
    private const string CacheKeyPrefix = "speech-credential:v1:";

    private static readonly FusionCacheEntryOptions CacheEntryOptions = new(TimeSpan.FromMinutes(10))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
    };

    private readonly VoiceDbContext _db;
    private readonly IVoiceProviderCredentialSettingsService _hostStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDataProtector _protector;
    private readonly IFusionCache _cache;
    private readonly ILogger<UnifiedSpeechCredentialResolver> _logger;

    public UnifiedSpeechCredentialResolver(
        VoiceDbContext db,
        IVoiceProviderCredentialSettingsService hostStore,
        ITenantProvider tenantProvider,
        IDataProtectionProvider dataProtectionProvider,
        IFusionCache cache,
        ILogger<UnifiedSpeechCredentialResolver> logger)
    {
        _db = db;
        _hostStore = hostStore;
        _tenantProvider = tenantProvider;
        _protector = dataProtectionProvider.CreateProtector(ApiKeyProtectionPurpose);
        _cache = cache;
        _logger = logger;
    }

    async Task<VoiceProviderCredentialResolution> IVoiceProviderCredentialResolver.ResolveAsync(
        string provider, CancellationToken cancellationToken)
    {
        var (apiKey, source, isTenantOverride) = await ResolveCoreAsync(provider, cancellationToken);
        return new VoiceProviderCredentialResolution(
            provider,
            apiKey,
            source,
            HasCredential: !string.IsNullOrWhiteSpace(apiKey),
            IsTenantOverride: isTenantOverride);
    }

    async Task<TextToSpeechProviderCredentialResolution> ITextToSpeechCredentialResolver.ResolveAsync(
        string provider, CancellationToken cancellationToken)
    {
        var (apiKey, source, isTenantOverride) = await ResolveCoreAsync(provider, cancellationToken);
        return new TextToSpeechProviderCredentialResolution(
            provider,
            apiKey,
            source,
            HasCredential: !string.IsNullOrWhiteSpace(apiKey),
            IsTenantOverride: isTenantOverride);
    }

    private async Task<(string? ApiKey, string Source, bool IsTenantOverride)> ResolveCoreAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        var normalisedVendor = provider.Trim().ToLowerInvariant();
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId)
            ? resolvedTenantId
            : (Guid?)null;

        if (tenantId.HasValue)
        {
            var providerKey = await GetCachedTenantProviderKeyAsync(
                normalisedVendor, tenantId.Value, cancellationToken);
            if (!string.IsNullOrWhiteSpace(providerKey))
            {
                return (providerKey, "TenantProvider", true);
            }
        }

        // Fall back to the host-default chain (host default → configuration). The legacy
        // service still owns this; we just don't use its tenant-override layer anymore.
        var hostResolution = await _hostStore.ResolveAsync(provider, cancellationToken);
        // The host store also reports "TenantOverride" when a tenant-level Setting row exists
        // from the legacy /settings/voice page. Post-Phase-D those Setting rows are deprecated;
        // but if they exist we still honour them so existing dev environments don't break.
        return (hostResolution.ApiKey, hostResolution.Source, hostResolution.IsTenantOverride);
    }

    private async Task<string?> GetCachedTenantProviderKeyAsync(
        string normalisedVendor,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId:N}:{normalisedVendor}";
        return await _cache.GetOrSetAsync<string?>(
            cacheKey,
            async ct => await LoadTenantProviderKeyAsync(normalisedVendor, tenantId, ct),
            CacheEntryOptions,
            cancellationToken);
    }

    private async Task<string?> LoadTenantProviderKeyAsync(
        string normalisedVendor,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // A vendor may have multiple rows (e.g. OpenAI as both STT + TTS); they share a
        // single credential. Look for any active row that actually has a key — most-recently-
        // updated first so a freshly-set key wins over a stale empty companion row.
        var encrypted = await _db.SpeechProviders
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && p.Vendor == normalisedVendor
                        && p.Status == SpeechProviderStatus.Active
                        && p.EncryptedApiKey != null)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Select(p => p.EncryptedApiKey)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch (Exception ex)
        {
            // Decryption can fail if the data protection key ring rotated and the old key is
            // gone. Log + treat as no-key so the fallback chain runs; admin can re-enter the
            // key in the UI.
            _logger.LogWarning(ex,
                "Failed to decrypt speech provider API key for tenant {TenantId} vendor {Vendor}; falling back to host default.",
                tenantId,
                normalisedVendor);
            return null;
        }
    }

    /// <summary>
    /// Cache invalidation hook the library service calls after writing a provider so the next
    /// resolution sees the new key without waiting for the 10-minute TTL.
    /// </summary>
    public async ValueTask InvalidateAsync(string vendor, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalised = vendor.Trim().ToLowerInvariant();
        var cacheKey = $"{CacheKeyPrefix}{tenantId:N}:{normalised}";
        await _cache.RemoveAsync(cacheKey, token: cancellationToken);
    }
}
