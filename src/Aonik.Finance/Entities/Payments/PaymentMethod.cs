using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// A tokenised, reusable payment instrument saved to a customer's vault (Spec 007).
/// Aonik stores ONLY the gateway's vault token plus non-sensitive display metadata
/// (brand, last four, expiry) — never a PAN, CVV, or any PCI-scoped data, which stay
/// with the PCI-compliant provider. Anemic per the AONIK entity rule: all behaviour
/// lives in <c>PaymentMethodService</c>.
/// </summary>
public class PaymentMethod : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Owning customer party. Resolved server-side from the authenticated user — never
    /// from request input — so one customer can never read or mutate another's vault.
    /// </summary>
    public Guid CustomerPartyId { get; set; }

    /// <summary>Provider that vaulted the instrument (an <c>IPaymentProviderGateway.ProviderCode</c>, e.g. "Stripe").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Opaque gateway vault token (e.g. a Stripe <c>pm_…</c>). The only credential Aonik holds; never a PAN.</summary>
    public string ProviderToken { get; set; } = string.Empty;

    /// <summary>Optional gateway-side customer handle (e.g. a Stripe <c>cus_…</c>) the token is attached to.</summary>
    public string? ProviderCustomerRef { get; set; }

    /// <summary>Instrument family — "card" for the first cut; left open for "bank_account", "wallet", …</summary>
    public string Type { get; set; } = "card";

    // ── Non-sensitive display metadata (safe to store; not PCI) ──────────
    /// <summary>Card network: visa, mastercard, verve, … (lower-cased).</summary>
    public string? Brand { get; set; }

    /// <summary>Last four digits only — never the full PAN.</summary>
    public string? Last4 { get; set; }

    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }

    /// <summary>Optional user nickname ("Personal Visa").</summary>
    public string? Label { get; set; }

    /// <summary>At most one default per customer; enforced by <c>PaymentMethodService</c> on save/delete.</summary>
    public bool IsDefault { get; set; }
}
