using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

/// <summary>
/// Links an order to the execution record that fulfils it - one of three typed nullable FKs
/// (<see cref="PayoutId"/>, <see cref="PaymentIntentId"/>, <see cref="PartnerBillPaymentId"/>) under
/// a DB CHECK that exactly one is set (configured in OrderFulfilmentRefConfiguration), so a
/// collection or a bill / airtime payment can fulfil an order, not only a payout.
/// </summary>
public class OrderFulfilmentRef : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }

    public Guid? PayoutId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public Guid? PartnerBillPaymentId { get; set; }
}
