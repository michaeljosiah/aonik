namespace Aonik.Platform.Contracts.Models.Settings;

public record PaymentGatewaySettingsSnapshot(
    IReadOnlyList<PaymentGatewayProviderSnapshot> Providers);

public record PaymentGatewayProviderSnapshot(
    string ProviderCode,
    bool Enabled,
    string BaseUrl,
    string IdpTokenUrl,
    string ClientId,
    string DefaultTransferPurpose,
    bool HasClientSecret,
    bool HasEncryptionKey,
    bool HasSigningSecret,
    string SecretSource);

public record PaymentGatewaySettingsUpdate(
    IReadOnlyList<PaymentGatewayProviderUpdate> Providers);

public record PaymentGatewayProviderUpdate(
    string ProviderCode,
    bool Enabled,
    string BaseUrl,
    string IdpTokenUrl,
    string ClientId,
    string DefaultTransferPurpose,
    string? ClientSecret,
    string? EncryptionKey,
    string? SigningSecret);

public record PaymentGatewayTestResult(
    bool Succeeded,
    string ProviderCode,
    string? ErrorMessage);
