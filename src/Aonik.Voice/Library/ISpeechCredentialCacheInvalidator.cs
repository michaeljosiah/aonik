namespace Aonik.Voice.Library;

/// <summary>
/// Side-channel for the speech provider library to nuke a cached credential when the provider
/// row's API key changes. Decouples <see cref="SpeechProviderLibraryService"/> from
/// <see cref="UnifiedSpeechCredentialResolver"/> so tests can supply a no-op implementation
/// without standing up the full caching stack.
/// </summary>
public interface ISpeechCredentialCacheInvalidator
{
    ValueTask InvalidateAsync(string vendor, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>No-op invalidator for tests + bootstrap scenarios where caching isn't wired.</summary>
internal sealed class NullSpeechCredentialCacheInvalidator : ISpeechCredentialCacheInvalidator
{
    public ValueTask InvalidateAsync(string vendor, Guid tenantId, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
