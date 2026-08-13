namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// The consent rules that apply to one jurisdiction (Spec 095 §5).
///
/// <para>
/// The threshold is data and the mechanism is code: the same child sits on either side of the line
/// depending on where they live, so building one consent machine capable of the strictest method and
/// letting jurisdiction decide when it triggers is the only shape that does not need rebuilding per
/// country.
/// </para>
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2, upper case.</param>
/// <param name="ConsentAge">Below this age a guardian must consent on the child's behalf.</param>
/// <param name="MajorityAge">When guardianship itself ends. Later than <paramref name="ConsentAge"/>,
/// and a different event — see Spec 095 §11.1.</param>
/// <param name="AcceptedMethods">Verification methods sufficient here, strongest first.</param>
public sealed record ConsentJurisdiction(
    string Code,
    int ConsentAge,
    int MajorityAge,
    IReadOnlyList<string> AcceptedMethods);

/// <summary>
/// Resolves which consent rules apply, from the <em>subscribing guardian's</em> jurisdiction.
///
/// <para>
/// Never from an IP address or a browser locale: both are trivially wrong and trivially spoofed, and
/// getting this wrong means processing a child's data under the wrong threshold.
/// </para>
/// </summary>
public interface IConsentJurisdictionResolver
{
    /// <summary>
    /// Resolve by country code. An unmapped or unknown code returns the <strong>strict default</strong>
    /// — 16, and the strictest method set — rather than a permissive fallback (Spec 095 §5).
    /// </summary>
    ConsentJurisdiction Resolve(string? countryCode);
}
