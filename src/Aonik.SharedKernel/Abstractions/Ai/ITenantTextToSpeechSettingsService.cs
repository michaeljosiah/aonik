namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ITenantTextToSpeechSettingsService
{
    Task<TextToSpeechSettings> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<TextToSpeechSettings> SaveCurrentAsync(
        TextToSpeechSettings settings,
        CancellationToken cancellationToken = default);
}
