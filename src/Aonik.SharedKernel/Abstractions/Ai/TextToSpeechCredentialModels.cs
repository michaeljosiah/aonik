namespace Aonik.SharedKernel.Abstractions.Ai;

public record TextToSpeechProviderCredentialResolution(
    string Provider,
    string? ApiKey,
    string Source,
    bool HasCredential,
    bool IsTenantOverride);
