using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

/// <summary>
/// Idempotent inbound-callback inbox across all four partner services (payout / collection / bill /
/// airtime) - the persisted twin of the translator's PartnerWebhookEvent output. Distinct from the
/// existing FinancialWebhookEvent (Plaid / personal-finance): it carries partner correlation that one
/// lacks - <see cref="Category"/> (service category), <see cref="ClientReference"/>, and
/// <see cref="SignatureValid"/>.
///
/// Global with a nullable <see cref="TenantId"/> - rows land on callback receipt, before a tenant is
/// resolved - and is purely global with NO tenant query filter, like FinancialWebhookEvent (not an
/// IsGlobalEntity() override). Deduped by two filtered unique indexes (see the EF configuration):
/// (ProviderCode, ProviderEventId) WHERE ProviderEventId IS NOT NULL, plus a fallback on
/// (ProviderCode, PayloadHash) WHERE ProviderEventId IS NULL.
/// </summary>
public class PartnerWebhookEvent : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>Service category the event belongs to (Payout | Collection | BillPayment | AirtimeTopup).</summary>
    public string Category { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    /// <summary>Provider-assigned event id used for dedupe; null for providers that do not supply one.</summary>
    public string? ProviderEventId { get; set; }

    public string ProviderReference { get; set; } = string.Empty;
    public string ClientReference { get; set; } = string.Empty;

    /// <summary>Hash of the raw payload - the fallback dedupe key when no ProviderEventId is supplied.</summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>Redacted raw payload - vendor codes and status only, never PANs / MSISDNs / secrets.</summary>
    public string RawPayload { get; set; } = string.Empty;

    public bool SignatureValid { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? Error { get; set; }
}
