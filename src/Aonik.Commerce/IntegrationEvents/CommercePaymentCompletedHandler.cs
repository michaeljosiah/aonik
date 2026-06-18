using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.IntegrationEvents;

/// <summary>
/// Consumes <see cref="PaymentCompletedEvent"/> (Spec 042 §11). When the completed payment funds a
/// Commerce checkout order, commits the held inventory, closes the cart, and transitions the order to
/// Complete. Discovered by the SharedKernel event-handler scan and invoked by the transactional
/// outbox dispatcher in the Worker, with the originating tenant restored — so the
/// <see cref="ICheckoutService"/> resolves the right tenant. Idempotent and a no-op for non-Commerce
/// orders.
/// </summary>
internal sealed class CommercePaymentCompletedHandler : IEventHandler<PaymentCompletedEvent>
{
    private readonly ICheckoutService _checkout;
    private readonly ILogger<CommercePaymentCompletedHandler> _logger;

    public CommercePaymentCompletedHandler(ICheckoutService checkout, ILogger<CommercePaymentCompletedHandler> logger)
    {
        _checkout = checkout;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.OrderId is not { } orderId)
        {
            return;
        }

        await _checkout.ConfirmPaymentAsync(orderId, cancellationToken);
        _logger.LogInformation("Commerce checkout confirmed for order {OrderId} on payment completion.", orderId);
    }
}
