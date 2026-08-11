using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

/// <summary>
/// Links an order to the execution record that fulfils it — one of four typed nullable FKs under a
/// DB CHECK that exactly one is set (configured in OrderFulfilmentRefConfiguration), so a
/// collection, a bill / airtime payment or a subscription period can fulfil an order, not only a
/// payout.
/// </summary>
public class OrderFulfilmentRef : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }

    public Guid? PayoutId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public Guid? PartnerBillPaymentId { get; set; }

    /// <summary>
    /// The subscription period this order delivered (Spec 087 §12).
    /// </summary>
    /// <remarks>
    /// The other three are money-movement records. A subscription renewal's fulfilment is a period
    /// of access, so it had no representable reference at all and the canonical order simply lost
    /// its fulfilment trace — the one thing the link exists to keep.
    /// </remarks>
    public Guid? SubscriptionPeriodId { get; set; }
}
