using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class Refund : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Original external cash receipt, when present.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Original funding intent; checkout returns may also identify their actual cash Payment.</summary>
    public Guid? PaymentIntentId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>Provider-specific lifecycle; storefront returns distinguish Requested, Unknown, Pending, Succeeded, Failed and NeedsReconciliation.</summary>
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

    public string? RequestSnapshotJson { get; set; }
    public DateTime? ProviderRequestStartedAtUtc { get; set; }
    public DateTime? EffectsAppliedAtUtc { get; set; }
    public Guid? GiftReclaimCardId { get; set; }
    public decimal GiftReclaimAmount { get; set; }
    public DateTime? GiftReclaimReleasedAtUtc { get; set; }
}
