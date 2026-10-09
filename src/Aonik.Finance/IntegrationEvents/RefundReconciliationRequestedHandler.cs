using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Finance.IntegrationEvents;

internal sealed class RefundReconciliationRequestedHandler(IRefundReconciler reconciler)
    : IEventHandler<RefundReconciliationRequestedEvent>
{
    public Task HandleAsync(RefundReconciliationRequestedEvent @event, CancellationToken cancellationToken = default)
        => reconciler.ReconcileAsync(@event.RefundId, @event.WebhookEventId, cancellationToken);
}
