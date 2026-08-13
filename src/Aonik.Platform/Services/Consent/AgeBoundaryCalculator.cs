using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.Platform.Entities.Party;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Computes the four scheduled boundaries from an attested date of birth (Spec 095 §6, §11.1).
///
/// <para>
/// The date of birth is an <em>input</em> and is never stored. What is stored are the dates the
/// boundaries fall on — which, as §6 says plainly, still reveal the birth date by arithmetic and are
/// therefore protected as birth-date-equivalent. The gain is in <em>use</em>: two fields each serving
/// one scheduled purpose, rather than a general-purpose birth date available to every query.
/// </para>
///
/// <para>
/// <strong>There is no fallback for a missing date.</strong> A year cannot say when a child turns 6,
/// 10 or 13 — a December-born child would enter looser safety bands eleven months early, and
/// majority could deactivate a guardian's authority before the young person is legally an adult. No
/// single default date is safe for all four boundaries, because they want opposite conservatism, so
/// enrolment refuses rather than inventing one.
/// </para>
/// </summary>
internal static class AgeBoundaryCalculator
{
    public sealed record AgeBoundaries(
        int BirthYear,
        DateTime ConsentAgeOn,
        DateTime MajorityOn,
        string ConsentBand,
        string SafetyBand,
        DateTime? SafetyBandChangesOn);

    public static AgeBoundaries Compute(
        DateOnly dateOfBirth,
        ConsentJurisdiction jurisdiction,
        DateTime asOf)
    {
        var consentAgeOn = AtAge(dateOfBirth, jurisdiction.ConsentAge);
        var majorityOn = AtAge(dateOfBirth, jurisdiction.MajorityAge);

        return new AgeBoundaries(
            BirthYear: dateOfBirth.Year,
            ConsentAgeOn: consentAgeOn,
            MajorityOn: majorityOn,
            ConsentBand: ResolveConsentBand(asOf, consentAgeOn, majorityOn),
            SafetyBand: ResolveSafetyBand(dateOfBirth, asOf, majorityOn),
            SafetyBandChangesOn: NextSafetyBandChange(dateOfBirth, asOf, majorityOn));
    }

    /// <summary>
    /// The instant a person born on <paramref name="dateOfBirth"/> reaches <paramref name="age"/>.
    /// A 29 February birth rolls to 1 March in non-leap years — the conservative direction for a
    /// safety band (they stay in the stricter one a day longer) and immaterial for the others.
    /// </summary>
    private static DateTime AtAge(DateOnly dateOfBirth, int age)
    {
        var year = dateOfBirth.Year + age;
        var day = dateOfBirth.Day;

        if (dateOfBirth.Month == 2 && day == 29 && !DateTime.IsLeapYear(year))
        {
            return new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        return new DateTime(year, dateOfBirth.Month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string ResolveConsentBand(DateTime asOf, DateTime consentAgeOn, DateTime majorityOn)
    {
        if (asOf >= majorityOn)
        {
            return PartyConsentBands.Adult;
        }

        // Between the consent threshold and majority the person consents for themselves AND still
        // has a guardian. Those are two different facts about one person, and collapsing them is the
        // error §11.1 exists to prevent.
        return asOf >= consentAgeOn
            ? PartyConsentBands.SelfConsenting
            : PartyConsentBands.BelowThreshold;
    }

    private static string ResolveSafetyBand(DateOnly dateOfBirth, DateTime asOf, DateTime majorityOn)
    {
        if (asOf >= majorityOn)
        {
            return PartySafetyBands.Adult;
        }

        // Safety banding tracks MINORITY, not consent capacity: acquiring the right to decide about
        // your own data does not stop you being someone these rules protect (Spec 096 §9).
        var band = PartySafetyBands.Default;

        foreach (var (fromAge, candidate) in PartySafetyBands.Boundaries)
        {
            if (asOf >= AtAge(dateOfBirth, fromAge))
            {
                band = candidate;
            }
        }

        return band;
    }

    private static DateTime? NextSafetyBandChange(DateOnly dateOfBirth, DateTime asOf, DateTime majorityOn)
    {
        foreach (var (fromAge, _) in PartySafetyBands.Boundaries)
        {
            var on = AtAge(dateOfBirth, fromAge);
            if (on > asOf)
            {
                return on;
            }
        }

        // Past the last child band, the next change is leaving childhood altogether.
        return majorityOn > asOf ? majorityOn : null;
    }
}
