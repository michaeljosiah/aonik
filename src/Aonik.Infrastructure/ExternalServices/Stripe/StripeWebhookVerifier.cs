using System.Security.Cryptography;
using System.Text;

using Stripe;
using Stripe.Checkout;

using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Infrastructure.ExternalServices.Stripe;

internal sealed class StripeWebhookVerifier : IStripeWebhookVerifier
{
    public VerifiedStripeWebhook Verify(string rawBody, string signature, IReadOnlyList<string> signingSecrets, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || Encoding.UTF8.GetByteCount(rawBody) > 256 * 1024
            || string.IsNullOrWhiteSpace(signature) || signature.Length > 4096 || signingSecrets.Count is < 1 or > 2)
        {
            throw new ArgumentException("Invalid Stripe webhook.");
        }

        Event? verified = null;
        foreach (var secret in signingSecrets)
        {
            try
            {
                verified = EventUtility.ConstructEvent(rawBody, signature, secret, tolerance: 300,
                    utcNow: new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                    throwOnApiVersionMismatch: true);
                break;
            }
            catch (Exception ex) when (ex is StripeException or System.Text.Json.JsonException or ArgumentException or FormatException)
            {
                // Try the unexpired previous signing secret, without exposing payloads or SDK errors.
            }
        }

        if (verified is null || string.IsNullOrWhiteSpace(verified.Id) || verified.Id.Length > 255
            || !string.IsNullOrEmpty(verified.Account) || !string.IsNullOrEmpty(verified.Context))
        {
            throw new ArgumentException("Invalid Stripe webhook.");
        }

        var session = verified.Data?.Object as Session;
        var payment = verified.Data?.Object as PaymentIntent;
        var supported = (session is not null && verified.Type is "checkout.session.completed"
            or "checkout.session.expired" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed")
            || (payment is not null && verified.Type is "payment_intent.processing" or "payment_intent.requires_action"
                or "payment_intent.payment_failed" or "payment_intent.succeeded" or "payment_intent.canceled");
        var metadata = session?.Metadata ?? payment?.Metadata;
        return new VerifiedStripeWebhook(verified.Id, verified.Type, verified.Livemode,
            ReadGuid(metadata, "paymentIntentId"), ReadGuid(metadata, "orderId"), ReadGuid(metadata, "tenantId"),
            ReadGuid(metadata, "connectorId"), session?.Id, session?.PaymentIntentId ?? payment?.Id,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))), supported);
    }

    private static Guid? ReadGuid(IDictionary<string, string>? metadata, string key)
        => metadata is not null && metadata.TryGetValue(key, out var value)
            && Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty ? id : null;
}
