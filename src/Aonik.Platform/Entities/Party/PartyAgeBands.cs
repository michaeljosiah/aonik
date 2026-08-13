namespace Aonik.Platform.Entities.Party;

/// <summary>
/// Who may consent (Spec 095 §6). Three-way partition, driven by the jurisdiction's consent age.
/// </summary>
public static class PartyConsentBands
{
    /// <summary>Below the jurisdiction's consent age. A guardian consents on their behalf.</summary>
    public const string BelowThreshold = "below-threshold";

    /// <summary>At or over the consent age, still a minor. Consents for themselves; still has a guardian.</summary>
    public const string SelfConsenting = "self-consenting";

    /// <summary>At or over majority. No guardian, no residual authority.</summary>
    public const string Adult = "adult";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BelowThreshold, SelfConsenting, Adult
    };
}

/// <summary>
/// What may be generated, and how strictly (Spec 096 §9). Four-way partition running to MAJORITY,
/// not to the consent threshold — a young person acquiring the right to decide about their own data
/// does not thereby stop being someone the safety rules protect. The two questions are unrelated.
///
/// <para>
/// An unknown age resolves to <see cref="Under6"/>, the strictest — not to <see cref="Adult"/>. The
/// wrong-way default is the one that ends badly, and being over-strict costs a support conversation
/// rather than an incident.
/// </para>
/// </summary>
public static class PartySafetyBands
{
    /// <summary>Curated only, no free text. Strictest thresholds; guardian review on by default.</summary>
    public const string Under6 = "under-6";

    /// <summary>Templates with bounded free text. Cartoon peril permitted; injury and darkness are not.</summary>
    public const string Age6To9 = "6-9";

    /// <summary>Free text with input classification. Real jeopardy allowed; graphic depiction is not.</summary>
    public const string Age10To12 = "10-12";

    /// <summary>Free text, teen-appropriate thresholds. Runs to majority.</summary>
    public const string Age13ToMajority = "13-to-majority";

    /// <summary>Not a child. Safety banding does not apply.</summary>
    public const string Adult = "adult";

    /// <summary>Applied when age is unknown. The strictest band, deliberately.</summary>
    public const string Default = Under6;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Under6, Age6To9, Age10To12, Age13ToMajority, Adult
    };

    /// <summary>The lower bound in years of each child band, used to compute transition dates.</summary>
    public static readonly IReadOnlyList<(int FromAge, string Band)> Boundaries = new List<(int, string)>
    {
        (0, Under6), (6, Age6To9), (10, Age10To12), (13, Age13ToMajority)
    };
}
