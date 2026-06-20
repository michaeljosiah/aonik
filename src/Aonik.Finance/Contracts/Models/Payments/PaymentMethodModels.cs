namespace Aonik.Finance.Contracts.Models.Payments;

/// <summary>
/// Result of starting a vault setup intent (Spec 007). Carries the client secret the frontend
/// provider SDK uses to collect and tokenise a card, plus the method types the provider accepts.
/// No card data is exchanged through Aonik — the SDK talks to the provider directly with this secret.
/// </summary>
public record SetupIntentResponse(
    string Provider,
    string ClientSecret,
    IReadOnlyList<string> PaymentMethodTypes,
    string SetupIntentReference,
    string? ProviderCustomerRef);

/// <summary>
/// Saves an already-tokenised instrument to the customer's vault. Carries ONLY the gateway token
/// and non-sensitive display metadata — never a raw PAN, CVV, or expiry-with-PAN. The owning
/// customer is resolved server-side from the caller, never from this request.
/// </summary>
public record SavePaymentMethodRequest(
    string ProviderToken,
    string? Provider = null,
    string Type = "card",
    string? Brand = null,
    string? Last4 = null,
    int? ExpiryMonth = null,
    int? ExpiryYear = null,
    string? Label = null,
    bool MakeDefault = false,
    string? ProviderCustomerRef = null);

/// <summary>A vaulted payment method as shown to the customer — masked display fields only.</summary>
public record PaymentMethodResponse(
    Guid Id,
    string Provider,
    string Type,
    string? Brand,
    string? Last4,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? Label,
    bool IsDefault,
    DateTime CreatedAt);
