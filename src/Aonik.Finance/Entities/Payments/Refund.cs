using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class Refund : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Card Payment being refunded; null when this reverses a partner collection (see <see cref="PaymentIntentId"/>).</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>PaymentIntent being reversed for a partner-collection refund (RefundCollectionAsync).</summary>
    public Guid? PaymentIntentId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>PartnerTransactionStatus vocabulary, stored as string.</summary>
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }

    // ── Partner-collection refund linkage (spec 031) ────────────────────────
    public Guid? ConnectorId { get; set; }

    /// <summary>Idempotent client reference for the refund.</summary>
    public string? ClientReference { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Redacted vendor response - codes and status only, never PANs / MSISDNs / secrets.</summary>
    public string? RawResponseJson { get; set; }
}
