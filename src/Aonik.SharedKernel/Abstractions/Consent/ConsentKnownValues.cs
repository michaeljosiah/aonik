namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// Consent purposes (Spec 095 §10.1). One grant per purpose — blanket consent is the failure mode
/// the shape exists to prevent. Everything except <see cref="ServiceCore"/> defaults to NOT granted,
/// per the Children's Code high-privacy-by-default standard.
/// </summary>
public static class ConsentPurposes
{
    /// <summary>Operating the account at all. Withdrawing it closes the account.</summary>
    public const string ServiceCore = "service-core";

    /// <summary>Sending the subject's inputs to named third-party model providers.</summary>
    public const string GenerationDisclosure = "generation-disclosure";

    /// <summary>
    /// Sending generated content to an external classifier so it can be judged before delivery
    /// (Spec 095 §12.3). Separate from <see cref="GenerationDisclosure"/> because content judged
    /// externally but authored locally is materially less disclosure — and honestly still some.
    /// </summary>
    public const string SafetyClassification = "safety-classification";

    /// <summary>Sharing the subject's work outside the family. Off by default.</summary>
    public const string SharingExternal = "sharing-external";

    /// <summary>Processing a recorded voice. Biometric-adjacent; its own decision.</summary>
    public const string Voice = "voice";

    /// <summary>Any use of the subject's content to improve models or the product. Opt-in only.</summary>
    public const string Improvement = "improvement";

    /// <summary>Promotional contact. Off by default, and never to a child.</summary>
    public const string Marketing = "marketing";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ServiceCore, GenerationDisclosure, SafetyClassification,
        SharingExternal, Voice, Improvement, Marketing
    };

    /// <summary>
    /// Purposes that cannot be refused without ending the service. Only <see cref="ServiceCore"/>:
    /// everything else is independently refusable, which is what makes the grants meaningful.
    /// </summary>
    public static readonly IReadOnlySet<string> NonRefusable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ServiceCore
    };
}

/// <summary>
/// How the consenting party was verified (Spec 095 §8). "Verifiable" is a term of art and a tickbox
/// is not one of these — a system recording agreement without evidencing who agreed has collected a
/// click, not a consent.
/// </summary>
public static class ConsentVerificationMethods
{
    /// <summary>A verified card or mandate held by the consenting adult, exercised through a real
    /// authorisation. The strongest available method and a by-product of paying.</summary>
    public const string PaymentInstrument = "payment-instrument";

    /// <summary>Document verification through the screening path. The document is deleted after the
    /// check; only the outcome is retained.</summary>
    public const string GovernmentId = "government-id";

    /// <summary>A returned form, electronically or physically signed, matched to a named adult.</summary>
    public const string SignedForm = "signed-form";

    /// <summary>
    /// The subject consenting for themselves, having authenticated (Spec 095 §11.3). Valid ONLY where
    /// the grantor equals the subject and they are at or over their jurisdiction's consent age. This
    /// is genuinely a verification, not the absence of one — they proved who they are by signing in.
    /// </summary>
    public const string SelfAuthenticated = "self-authenticated";

    /// <summary>Predates any verification. Carried only on the legacy archive, never on a
    /// <c>ConsentGrant</c>, and authorises nothing.</summary>
    public const string LegacyUnverified = "legacy-unverified";

    /// <summary>Methods valid on a ConsentGrant. Excludes <see cref="LegacyUnverified"/>.</summary>
    public static readonly IReadOnlySet<string> Grantable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PaymentInstrument, GovernmentId, SignedForm, SelfAuthenticated
    };

    /// <summary>
    /// Methods that verify one adult acting for another party. <see cref="SelfAuthenticated"/> is
    /// deliberately absent: it cannot be used to consent on anyone else's behalf.
    /// </summary>
    public static readonly IReadOnlySet<string> Parental = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PaymentInstrument, GovernmentId, SignedForm
    };
}

/// <summary>Why an active grant stopped being active.</summary>
public static class ConsentRevocationReasons
{
    public const string Withdrawn = "withdrawn";
    public const string TermsSuperseded = "terms-superseded";
    public const string AgeUpLapse = "age-up-lapse";
    public const string AccountClosed = "account-closed";
}
