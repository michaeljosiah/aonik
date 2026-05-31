using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// The executed bill / airtime transaction - the bill-side analogue of <see cref="Payout"/>.
/// Persists the vend <see cref="VendToken"/>, provider reference, normalized status, and the order
/// item it fulfils. <see cref="ServiceCategory"/> distinguishes a bill payment from an airtime / data
/// top-up so the same table backs both. Named PartnerBillPayment (not BillPayment) to avoid colliding
/// with PersonalFinance's Bill obligation entity and with the bill-payment port. Tenant-scoped.
/// </summary>
public class PartnerBillPayment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid ConnectorId { get; set; }
    public Guid? ConnectorBillerMappingId { get; set; }
    public string BillerCode { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ClientReference { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public Guid? BillValidationId { get; set; }

    /// <summary>BillPayment | AirtimeTopup.</summary>
    public string ServiceCategory { get; set; } = string.Empty;

    /// <summary>Vend token / PIN returned by the biller (e.g. a prepaid-electricity token).</summary>
    public string? VendToken { get; set; }

    /// <summary>PartnerTransactionStatus vocabulary, stored as string.</summary>
    public string Status { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    /// <summary>Redacted vendor response - codes and status only, never PANs / MSISDNs / secrets.</summary>
    public string? RawResponseJson { get; set; }
}
