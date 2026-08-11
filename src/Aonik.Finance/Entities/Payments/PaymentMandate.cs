using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// A customer's standing authorisation to charge a vaulted instrument without them present
/// (Spec 088 §6) — what a renewal job needs and the guest-intent path cannot provide.
///
/// The authorisation belongs to a <b>party</b>, not to a payment method: a card can be reissued
/// while the customer's consent persists, and tying the mandate to the method would force
/// re-authorisation for what is really a bank's administrative act.
/// </summary>
public class PaymentMandate : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Who authorised the charging. A party may hold several mandates.</summary>
    public Guid PartyId { get; set; }

    /// <summary>The provider holding the vaulted instrument.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The vaulted <see cref="PaymentMethod"/> this mandate charges.</summary>
    public Guid PaymentMethodId { get; set; }

    /// <summary>The provider's own recurring-authorisation token, where it issues one.</summary>
    public string? ProviderMandateRef { get; set; }

    /// <summary>One of <c>PaymentMandateStatuses</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the customer gave the authorisation. Always an interactive moment.</summary>
    public DateTime AuthorisedAt { get; set; }

    /// <summary>Card or provider-mandate expiry, when known. Treated exactly as revoked once passed.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Why it was revoked — customer request, provider notification, card replaced.</summary>
    public string? RevocationReason { get; set; }
}
