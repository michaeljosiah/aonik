namespace Aonik.Platform.Contracts.Models.Settings;

public record TextToSpeechCredentialSnapshot(
    string Provider,
    bool HasHostCredential,
    bool HasTenantOverride,
    string EffectiveSource);

public record TextToSpeechCredentialUpdate(
    string Provider,
    string? ApiKey,
    bool ClearStoredValue);
