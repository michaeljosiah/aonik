namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ITextToSpeechCredentialResolver
{
    Task<TextToSpeechProviderCredentialResolution> ResolveAsync(
        string provider,
        CancellationToken cancellationToken = default);
}
