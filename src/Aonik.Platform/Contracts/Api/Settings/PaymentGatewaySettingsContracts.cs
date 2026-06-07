namespace Aonik.Platform.Contracts.Api.Settings;

public record PaymentGatewaySettingsResponse(
    IReadOnlyList<PaymentGatewayProviderResponse> Providers);

public record PaymentGatewayProviderResponse(
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

public record PaymentGatewaySettingsUpdateRequest(
    IReadOnlyList<PaymentGatewayProviderUpdateRequest> Providers);

public record PaymentGatewayProviderUpdateRequest(
    string ProviderCode,
    bool Enabled,
    string BaseUrl,
    string IdpTokenUrl,
    string ClientId,
    string DefaultTransferPurpose,
    string? ClientSecret,
    string? EncryptionKey,
    string? SigningSecret);

public record TestPaymentGatewayRequest(string ProviderCode);

public record TestPaymentGatewayResponse(
    bool Succeeded,
    string ProviderCode,
    string? ErrorMessage);
