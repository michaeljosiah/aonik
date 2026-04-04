using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface ITextToSpeechCredentialSettingsService : ITextToSpeechCredentialResolver
{
    Task<TextToSpeechCredentialSnapshot> GetHostAsync(
        string provider,
        CancellationToken cancellationToken = default);

    Task<TextToSpeechCredentialSnapshot> SaveHostAsync(
        TextToSpeechCredentialUpdate update,
        CancellationToken cancellationToken = default);

    Task<TextToSpeechCredentialSnapshot> GetTenantAsync(
        string provider,
        CancellationToken cancellationToken = default);

    Task<TextToSpeechCredentialSnapshot> SaveTenantAsync(
        TextToSpeechCredentialUpdate update,
        CancellationToken cancellationToken = default);
}
