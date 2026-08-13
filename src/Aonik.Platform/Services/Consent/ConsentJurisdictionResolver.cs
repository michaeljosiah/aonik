using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.Platform.Entities.Party;
using Microsoft.Extensions.Options;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §5. Resolves the consent rules for a jurisdiction, defaulting <strong>strict</strong>.
/// </summary>
internal sealed class ConsentJurisdictionResolver : IConsentJurisdictionResolver
{
    private readonly IReadOnlyDictionary<string, ConsentJurisdiction> _known;
    private readonly ConsentJurisdiction _default;

    public ConsentJurisdictionResolver(IOptions<ConsentOptions> options)
    {
        var configured = options.Value;

        _default = new ConsentJurisdiction(
            Code: "??",
            ConsentAge: configured.DefaultConsentAge,
            MajorityAge: configured.DefaultMajorityAge,
            AcceptedMethods: StrictestMethods);

        var map = new Dictionary<string, ConsentJurisdiction>(StringComparer.OrdinalIgnoreCase);

        foreach (var jurisdiction in BuiltIn)
        {
            map[jurisdiction.Code] = jurisdiction;
        }

        // A tenant may be STRICTER than the statute and never laxer (Spec 095 §5). Applied here so
        // the rule holds wherever the resolver is used, rather than at each call site.
        foreach (var over in configured.Jurisdictions)
        {
            if (!map.TryGetValue(over.Code, out var statutory))
            {
                map[over.Code] = new ConsentJurisdiction(
                    over.Code.ToUpperInvariant(), over.ConsentAge, over.MajorityAge, StrictestMethods);
                continue;
            }

            map[over.Code] = statutory with
            {
                ConsentAge = Math.Max(statutory.ConsentAge, over.ConsentAge),
                MajorityAge = Math.Max(statutory.MajorityAge, over.MajorityAge)
            };
        }

        _known = map;
    }

    /// <summary>
    /// Only the parental methods. <c>self-authenticated</c> is deliberately absent: it cannot be used
    /// to consent on anyone else's behalf (Spec 095 §11.3).
    /// </summary>
    private static readonly IReadOnlyList<string> StrictestMethods = new[]
    {
        ConsentVerificationMethods.PaymentInstrument,
        ConsentVerificationMethods.GovernmentId
    };

    private static readonly IReadOnlyList<string> StandardMethods = new[]
    {
        ConsentVerificationMethods.PaymentInstrument,
        ConsentVerificationMethods.GovernmentId,
        ConsentVerificationMethods.SignedForm
    };

    /// <summary>
    /// Launch jurisdictions only. Adding one is a legal review, not a config row — so the built-in
    /// set stays deliberately short and everything else takes the strict default.
    /// </summary>
    private static readonly IReadOnlyList<ConsentJurisdiction> BuiltIn = new[]
    {
        new ConsentJurisdiction("GB", ConsentAge: 13, MajorityAge: 18, StandardMethods)
    };

    public ConsentJurisdiction Resolve(string? countryCode)
    {
        // The strict default is the whole point of this method, and it is the opposite of how
        // defaults are usually chosen. A wrong-way default here means processing a child's data
        // under a threshold nobody checked, discovered through a regulator. Being over-strict costs
        // friction for some families; being under-strict is a breach.
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return _default;
        }

        var normalized = countryCode.Trim();

        return _known.TryGetValue(normalized, out var jurisdiction)
            ? jurisdiction
            : _default with { Code = normalized.ToUpperInvariant() };
    }
}

/// <summary>Configuration for <see cref="ConsentJurisdictionResolver"/>.</summary>
public sealed class ConsentOptions
{
    public const string SectionName = "Consent";

    /// <summary>
    /// Applied to any jurisdiction not explicitly mapped. 16 — the GDPR Article 8 default before a
    /// member state lowers it — because assuming the lower age for an unknown country is the
    /// mistake that ends in a breach.
    /// </summary>
    public int DefaultConsentAge { get; set; } = 16;

    public int DefaultMajorityAge { get; set; } = 18;

    /// <summary>Tenant overrides. Raised only; a configured age below the statutory one is ignored.</summary>
    public List<ConsentJurisdictionOptions> Jurisdictions { get; set; } = new();
}

public sealed class ConsentJurisdictionOptions
{
    public string Code { get; set; } = string.Empty;
    public int ConsentAge { get; set; }
    public int MajorityAge { get; set; }
}
