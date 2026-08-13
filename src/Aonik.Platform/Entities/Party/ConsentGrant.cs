using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// A verifiable consent record (Spec 095 §10). Replaces <see cref="PartyConsent"/>, which cannot
/// record who consented, how they were verified, for what purpose, or under which terms — and is
/// therefore retired in stages rather than dropped, because it has live export/import consumers.
///
/// One row per purpose. Blanket consent is the failure mode this shape exists to prevent.
/// </summary>
public class ConsentGrant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Who the consent is about — the child, or an adult consenting for themselves.</summary>
    public Guid SubjectPartyId { get; set; }

    /// <summary>
    /// Who consented. For a guardian grant this is the guardian; for a self-grant it equals
    /// <see cref="SubjectPartyId"/>, and that equality <em>is</em> the marker of a self-grant
    /// (Spec 095 §11.3) — there is no separate flag to forget to set.
    /// </summary>
    public Guid GrantedByPartyId { get; set; }

    /// <summary>One of <see cref="ConsentPurposes"/>.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Which terms text was agreed to. Publishing a new current version revokes affected active
    /// grants at publication (Spec 095 §10.2) — it does not wait for a replacement, because waiting
    /// means processing under withdrawn terms for everyone who never replies.
    /// </summary>
    public string TermsVersion { get; set; } = string.Empty;

    /// <summary>Which threshold and method set applied at the time. ISO 3166-1 alpha-2.</summary>
    public string Jurisdiction { get; set; } = string.Empty;

    /// <summary>One of <see cref="ConsentVerificationMethods"/>.</summary>
    public string VerificationMethod { get; set; } = string.Empty;

    /// <summary>
    /// Pointer to a verification <em>outcome</em> — a payment authorisation id, a check result id.
    /// Never the document, card number or recording itself (Spec 095 §13).
    /// </summary>
    public string? VerificationRef { get; set; }

    public DateTime VerifiedAt { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByPartyId { get; set; }

    /// <summary>Why it was revoked — withdrawal, terms superseded, or age-up lapse.</summary>
    public string? RevocationReason { get; set; }
}
