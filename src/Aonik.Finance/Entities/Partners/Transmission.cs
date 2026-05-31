using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// One attempt to push a money-movement instruction to a connector. The target is one of three
/// typed nullable FKs - <see cref="PayoutId"/>, <see cref="PaymentIntentId"/>,
/// <see cref="PartnerBillPaymentId"/> - under a DB CHECK that exactly one is set (configured in
/// TransmissionConfiguration). Typed FKs preserve referential integrity that a polymorphic
/// TargetType + TargetId pair would drop.
/// </summary>
public class Transmission : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? PayoutId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public Guid? PartnerBillPaymentId { get; set; }

    public Guid ConnectorId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Provider-assigned reference for this attempt, for reconciliation / requery.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>PartnerTransactionStatus vocabulary, stored as string.</summary>
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }

    /// <summary>Redacted vendor response - codes and status only, never PANs / MSISDNs / secrets.</summary>
    public string? RawResponseJson { get; set; }
}
