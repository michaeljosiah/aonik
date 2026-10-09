using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Finance.IntegrationEvents;

internal sealed class CheckoutPaymentReconciliationRequestedHandler(ICheckoutPaymentReconciler reconciler)
    : IEventHandler<CheckoutPaymentReconciliationRequestedEvent>
{
    public async Task HandleAsync(CheckoutPaymentReconciliationRequestedEvent @event, CancellationToken cancellationToken = default)
    {
        await reconciler.ReconcileAsync(@event.PaymentIntentId, webhookEventId: @event.WebhookEventId, cancellationToken: cancellationToken);
    }
}
