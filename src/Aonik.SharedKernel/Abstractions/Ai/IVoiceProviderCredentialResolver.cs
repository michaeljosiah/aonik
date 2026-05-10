namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Resolves a voice provider's API key for the current tenant. Mirrors
/// <see cref="ITextToSpeechCredentialResolver"/> — tenant override beats host
/// default beats configuration fallback. Used by
/// <c>Aonik.Voice.AonikVoicePipelineFactory</c> instead of reading
/// <c>IConfiguration</c> directly so credential storage and resolution stay
/// inside Platform.
/// </summary>
public interface IVoiceProviderCredentialResolver
{
    Task<VoiceProviderCredentialResolution> ResolveAsync(
        string provider,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of resolving a voice provider credential. Includes the source so
/// observability can distinguish "tenant override" from "host default" from
/// "missing".
/// </summary>
public sealed record VoiceProviderCredentialResolution(
    string Provider,
    string? ApiKey,
    string Source,
    bool HasCredential,
    bool IsTenantOverride);
