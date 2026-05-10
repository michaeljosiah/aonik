namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Status-only readback for a voice provider credential. Mirrors AONIK's
/// existing TTS credential snapshot. Never echoes raw API keys — admins see
/// whether one is configured, not the value.
/// </summary>
public sealed record VoiceProviderCredentialSnapshot(
    string Provider,
    bool HasHostCredential,
    bool HasTenantOverride,
    string EffectiveSource);

/// <summary>
/// Update payload for a voice provider credential. Set
/// <see cref="ClearStoredValue"/> to remove the existing value; otherwise
/// supply a new <see cref="ApiKey"/>.
/// </summary>
public sealed record VoiceProviderCredentialUpdate(
    string Provider,
    string? ApiKey,
    bool ClearStoredValue);

/// <summary>
/// Voice provider credential CRUD. Lives on SharedKernel (not Platform.Contracts)
/// because <c>Aonik.Voice</c> consumes it and modules don't reference each
/// other directly. Mirrors AONIK's existing TTS credential service shape —
/// host-level defaults + per-tenant overrides + status-only readback.
/// </summary>
public interface IVoiceProviderCredentialSettingsService : IVoiceProviderCredentialResolver
{
    Task<VoiceProviderCredentialSnapshot> GetHostAsync(
        string provider,
        CancellationToken cancellationToken = default);

    Task<VoiceProviderCredentialSnapshot> SaveHostAsync(
        VoiceProviderCredentialUpdate update,
        CancellationToken cancellationToken = default);

    Task<VoiceProviderCredentialSnapshot> GetTenantAsync(
        string provider,
        CancellationToken cancellationToken = default);

    Task<VoiceProviderCredentialSnapshot> SaveTenantAsync(
        VoiceProviderCredentialUpdate update,
        CancellationToken cancellationToken = default);
}
