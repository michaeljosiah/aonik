using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Finance.Services.Payments;

internal class StripeSimulatedPaymentProviderGateway : IPaymentProviderGateway
{
    private static readonly IReadOnlyList<string> SupportedPaymentMethodTypes = ["card"];

    public string ProviderCode => "Stripe";

    public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(
        PaymentProviderSetupIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Mirrors Stripe's SetupIntent shape: a "seti_…" reference and a "{ref}_secret_…" client
        // secret the frontend SDK confirms with a card to mint a reusable payment method off-platform.
        var suffix = Guid.NewGuid().ToString("N")[..24];
        var setupIntentReference = $"seti_{suffix}";
        var clientSecret = $"{setupIntentReference}_secret_{Guid.NewGuid():N}";

        var customerRef = string.IsNullOrWhiteSpace(request.ProviderCustomerRef)
            ? $"cus_{Guid.NewGuid().ToString("N")[..14]}"
            : request.ProviderCustomerRef;

        var result = new PaymentProviderSetupIntentResult(
            ProviderCode,
            setupIntentReference,
            clientSecret,
            SupportedPaymentMethodTypes,
            customerRef);

        return Task.FromResult(result);
    }

    public Task<PaymentProviderIntentResult> CreateIntentAsync(
        PaymentProviderIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var suffix = Guid.NewGuid().ToString("N")[..24];
        var providerReference = $"pi_{suffix}";
        var clientSecret = $"{providerReference}_secret_{Guid.NewGuid():N}";

        var checkoutUrl = BuildCheckoutUrl(request.ReturnUrl, providerReference);

        var result = new PaymentProviderIntentResult(
            ProviderCode,
            providerReference,
            "Pending",
            clientSecret,
            checkoutUrl);

        return Task.FromResult(result);
    }

    private static string? BuildCheckoutUrl(string? returnUrl, string providerReference)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}provider=stripe&payment_intent={providerReference}";
    }
}
