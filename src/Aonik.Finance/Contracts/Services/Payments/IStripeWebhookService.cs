namespace Aonik.Finance.Contracts.Services.Payments;

public interface IStripeWebhookService
{
    Task AcceptAsync(Guid connectorId, string rawBody, string signature, CancellationToken cancellationToken = default);
}
