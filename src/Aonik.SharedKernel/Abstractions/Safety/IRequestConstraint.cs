namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// L1 — what may be asked at all (Spec 096 §7).
///
/// <para>
/// <strong>Narrowing what can be generated beats filtering what was.</strong> A filter is an
/// adversarial contest against a model's entire output distribution, run in milliseconds, on every
/// request, forever. A constrained request space is a design decision made once. The second is more
/// reliable and vastly cheaper, and it is underused because filtering feels like the more direct
/// answer.
/// </para>
///
/// <para>
/// This layer runs <em>before</em> classification and before any spend, so a request the band does
/// not permit never becomes a model call.
/// </para>
/// </summary>
public interface IRequestConstraint
{
    /// <summary>
    /// Whether this band may submit this shape of request at all.
    /// </summary>
    Task<ConstraintVerdict> EvaluateAsync(
        ConstrainedRequest request,
        CancellationToken cancellationToken = default);
}

/// <param name="FreeText">
/// What the child typed, if anything. Null when the request was assembled entirely from curated
/// choices — which is the shape the youngest band is limited to.
/// </param>
/// <param name="TemplateId">The story template, when one was used.</param>
/// <param name="CharacterIds">Curated characters chosen. Removes the real-person-likeness category outright.</param>
public sealed record ConstrainedRequest(
    Guid SubjectPartyId,
    string SafetyBand,
    string? FreeText = null,
    string? TemplateId = null,
    IReadOnlyList<string>? CharacterIds = null);

/// <param name="Reason">
/// For the operator and the audit record — never shown to the child verbatim. A seven-year-old told
/// they broke a rule learns they did something wrong; they did not.
/// </param>
public sealed record ConstraintVerdict(bool Allowed, string? Reason = null)
{
    public static readonly ConstraintVerdict Allow = new(true);

    public static ConstraintVerdict Refuse(string reason) => new(false, reason);
}

/// <summary>
/// What each band may submit (Spec 096 §9, §7).
///
/// <para>
/// The under-6 rule is the one that matters, and it is a product decision as much as an engineering
/// one: <strong>free-text prompting by young children is the single riskiest feature in Arke
/// Kids</strong>. It is worth asking whether the youngest band needs it at all rather than assuming
/// it and then defending it with classifiers — and the answer taken here is that they do not. They
/// are better served by a beautiful curated experience than an open box.
/// </para>
/// </summary>
public sealed record BandConstraints(
    bool AllowsFreeText,
    int MaxFreeTextLength,
    bool RequiresTemplate,
    bool RequiresCuratedCharacters)
{
    public static BandConstraints For(string safetyBand) => safetyBand switch
    {
        // Curated only. No free text, so the real-person-likeness category is unreachable and the
        // frightening-figure category is largely so.
        SafetyBandNames.Under6 => new(false, 0, RequiresTemplate: true, RequiresCuratedCharacters: true),

        // Templates with bounded free text: structure supplied, the child supplies the variables.
        SafetyBandNames.Age6To9 => new(true, 120, RequiresTemplate: true, RequiresCuratedCharacters: true),

        // Free text with input classification behind it.
        SafetyBandNames.Age10To12 => new(true, 500, RequiresTemplate: false, RequiresCuratedCharacters: false),

        SafetyBandNames.Age13ToMajority => new(true, 2000, RequiresTemplate: false, RequiresCuratedCharacters: false),

        // An unknown band takes the strictest constraints, not the loosest — the same wrong-way
        // default rule as everywhere else in this design.
        _ => new(false, 0, RequiresTemplate: true, RequiresCuratedCharacters: true),
    };
}

/// <summary>
/// Band names as SharedKernel knows them, so the constraint table and the gate agree without either
/// referencing a module.
/// </summary>
public static class SafetyBandNames
{
    public const string Under6 = "under-6";
    public const string Age6To9 = "6-9";
    public const string Age10To12 = "10-12";
    public const string Age13ToMajority = "13-to-majority";
    public const string Adult = "adult";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Under6, Age6To9, Age10To12, Age13ToMajority, Adult
    };
}
