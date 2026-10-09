namespace Aonik.Finance.Contracts.Services.Payments;

public interface IStripeWebhookVerifier
{
    VerifiedStripeWebhook Verify(string rawBody, string signature,
        IReadOnlyList<string> signingSecrets, DateTime utcNow);
}
