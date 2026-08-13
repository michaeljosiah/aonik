using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// An operator's record that a guardian was verified <strong>by a human, out of band</strong>
/// (Spec 095 §8, signed-form method).
///
/// <para>
/// The signed-form method is inherently manual: a form is returned, someone reads it, and someone
/// matches it to a named adult. The platform's job is therefore not to <em>perform</em> that check
/// but to hold the evidence that it happened — who attested, when, and against what reference. A
/// verifier that tried to automate this would be inventing an outcome.
/// </para>
///
/// <para>
/// It is deliberately expiring. An attestation is a statement about a moment; treating one made
/// four years ago as current verification is how a manual process quietly becomes no process.
/// </para>
/// </summary>
public class GuardianAttestation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The adult who was verified.</summary>
    public Guid GuardianPartyId { get; set; }

    /// <summary>One of <c>ConsentVerificationMethods</c> — the method the human actually used.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The staff member who attests. Named rather than a role: "someone in support checked it" is
    /// not an attestation, and this is the field that makes it one.
    /// </summary>
    public Guid AttestedByUserId { get; set; }

    /// <summary>
    /// A reference to the evidence, held wherever the operator's process holds it — a case number,
    /// a document management id. <strong>Never the document itself</strong>: §13 retains outcomes
    /// and no evidence, and that applies to the manual path exactly as it does to the automated one.
    /// </summary>
    public string? EvidenceRef { get; set; }

    public string? Notes { get; set; }

    public DateTime AttestedAt { get; set; }

    /// <summary>When this stops counting as current verification.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}
