using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class Party : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CustomerTierCode { get; set; }

    // ── Age (Spec 095 §6, §11.1) ─────────────────────────────────────────────
    // The attested date of birth is used to compute the fields below and is NOT stored.
    //
    // That is a minimisation gain in USE, not in kind: subtracting a jurisdiction's threshold from
    // ConsentAgeOn recovers the exact date. These fields are therefore classified and protected as
    // birth-date-equivalent personal data — same access controls, retention and DPIA treatment.

    /// <summary>Birth year, for coarse reporting. Null for parties whose age is not relevant.</summary>
    public int? BirthYear { get; set; }

    /// <summary>
    /// Who may consent for this party: one of <see cref="PartyConsentBands"/>. Deliberately separate
    /// from <see cref="SafetyBand"/> — they answer unrelated questions, change on different dates,
    /// and are set by different authorities (a legislature, and our own product judgement).
    /// </summary>
    public string? ConsentBand { get; set; }

    /// <summary>What may be generated for this party, and how strictly: one of
    /// <see cref="PartySafetyBands"/> (Spec 096 §9). Not derivable from <see cref="ConsentBand"/>.</summary>
    public string? SafetyBand { get; set; }

    /// <summary>When guardian <em>consent</em> authority lapses and this party consents for
    /// themselves. Earlier than <see cref="MajorityOn"/>, and not the same event.</summary>
    public DateTime? ConsentAgeOn { get; set; }

    /// <summary>When the <c>Guardian</c> edge itself ends. Guardianship outlives the consent
    /// threshold — in the UK by five years.</summary>
    public DateTime? MajorityOn { get; set; }

    /// <summary>When this party next moves safety band. Recomputed on each transition.</summary>
    public DateTime? SafetyBandChangesOn { get; set; }

    /// <summary>
    /// When advance notice of an upcoming age transition was sent (Spec 095 §11). Idempotency
    /// marker: without it a daily cron would notify every day for a month, turning a considerate
    /// feature into a nuisance.
    /// </summary>
    public DateTime? AgeTransitionNoticeSentOn { get; set; }

    public List<PartyAddress> Addresses { get; set; } = new();
    public List<PartyContact> Contacts { get; set; } = new();

    /// <summary>Legacy, pre-Spec-095. Retired in stages; <c>ConsentGrant</c> is the live record.</summary>
    public List<PartyConsent> Consents { get; set; } = new();
}
