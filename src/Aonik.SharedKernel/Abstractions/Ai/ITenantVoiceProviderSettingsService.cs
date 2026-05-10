namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Tenant voice provider settings — mirrors
/// <see cref="ITenantTextToSpeechSettingsService"/>'s shape so admin UI work
/// can reuse the same patterns. Persisted as a JSON payload on the existing
/// generic <c>Settings</c> table (no EF migration in v1).
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 2.
/// </para>
/// </summary>
public interface ITenantVoiceProviderSettingsService
{
    /// <summary>
    /// Returns the current tenant's voice provider configuration, or
    /// <see cref="VoiceProviderConfiguration.Disabled"/> when nothing is
    /// persisted yet.
    /// </summary>
    Task<VoiceProviderConfiguration> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied configuration for the current tenant and returns
    /// the saved value (post any normalisation).
    /// </summary>
    Task<VoiceProviderConfiguration> SaveCurrentAsync(
        VoiceProviderConfiguration settings,
        CancellationToken cancellationToken = default);
}
