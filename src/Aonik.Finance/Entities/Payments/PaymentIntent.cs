using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class PaymentIntent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Party funding the payment, resolved from the linked order (the canonical
    /// holder of the payer) or an explicit override. Null models a genuine "no
    /// payer yet" draft — it is never written as <c>Guid.Empty</c>. A real payer
    /// is required before the intent can be authorized (the money-movement step).
    /// </summary>
    public Guid? PayerPartyId { get; set; }
    public Guid? PayeePartyId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string PurposeType { get; set; } = string.Empty;
    public Guid PurposeId { get; set; }

    /// <summary>
    /// Rail used to fund the payment (e.g. "Card", "BankTransfer"). Null models an
    /// unspecified method on a draft — it is never silently defaulted to "Card". A
    /// concrete method is required before the intent can be authorized.
    /// </summary>
    public string? PaymentMethodType { get; set; }
    public string? PaymentMethodRef { get; set; }

    /// <summary>
    /// Card-checkout lifecycle (PaymentStatus vocabulary), parsed and enforced by PaymentService.
    /// A partner status must NOT be written here - the partner outcome rides <see cref="CollectionStatus"/>.
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }

    // ── Partner-collection linkage (spec 031) ───────────────────────────────
    public Guid? ConnectorId { get; set; }

    /// <summary>Idempotent client reference for a partner-initiated collection (tx_ref / clientRef).</summary>
    public string? ClientReference { get; set; }

    /// <summary>Provider-assigned reference (flw_ref / paymentRef).</summary>
    public string? ProviderReference { get; set; }

    /// <summary>Card | BankTransfer | MobileMoney | Ussd - the collection rail.</summary>
    public string? CollectionMethod { get; set; }

    public string? MobileNetwork { get; set; }

    /// <summary>Masked payer MSISDN - the full number is carried only on the transient CollectionInstruction.</summary>
    public string? MaskedPhoneNumber { get; set; }

    /// <summary>Relayed next-action: redirect | ussd | pin.</summary>
    public string? NextActionMode { get; set; }
    public string? NextActionRedirectUrl { get; set; }
    public string? NextActionUssdCode { get; set; }

    public decimal? SettledAmount { get; set; }
    public decimal? Fee { get; set; }
    public decimal? FxRate { get; set; }

    /// <summary>Partner-collection outcome (PartnerTransactionStatus vocabulary), stored beside <see cref="Status"/>.</summary>
    public string? CollectionStatus { get; set; }
}
