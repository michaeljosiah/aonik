namespace Aonik.Finance.Contracts.Services.Payments;

public interface IRefundReconciler
{
    Task ReconcileAsync(Guid refundId, Guid? webhookEventId = null, CancellationToken cancellationToken = default);
}
