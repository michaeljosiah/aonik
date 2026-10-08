namespace Aonik.Commerce.Services.Checkout;

public interface IOrderConfirmationEmailService
{
    Task SendAsync(Guid orderId, Guid completedPaymentIntentId, CancellationToken cancellationToken = default);
}
