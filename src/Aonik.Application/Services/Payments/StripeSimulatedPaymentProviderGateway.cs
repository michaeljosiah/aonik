namespace Aonik.Application.Services.Payments;

public class StripeSimulatedPaymentProviderGateway : IPaymentProviderGateway
{
    public string ProviderCode => "Stripe";

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
