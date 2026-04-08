namespace Aonik.Platform.Contracts.Api.Settings;

public record GetTenantTextToSpeechCredentialRequest(string? Provider);

public record TextToSpeechCredentialResponse(
    string Provider,
    bool HasHostCredential,
    bool HasTenantOverride,
    string EffectiveSource);

public record TextToSpeechCredentialUpdateRequest(
    string Provider,
    string? ApiKey,
    bool ClearStoredValue);
