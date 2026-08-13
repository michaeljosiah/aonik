namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// The authoritative safety band for a party, from Spec 095's attested dates.
///
/// <para>
/// <strong>The band must never come from the caller.</strong> A request that carries its own band is a
/// request that can claim <c>adult</c> for a six-year-old — skipping every threshold and every
/// guardian hold with one field. It is the same class of mistake as trusting a client to say whether
/// it checked: the value has to be read from the record maintained by the age-transition job, not
/// asserted by whoever is asking.
/// </para>
///
/// <para>
/// Lives in SharedKernel because the band is written by <c>Aonik.Platform</c> and read by
/// <c>Aonik.Ai</c>, and neither may reference the other.
/// </para>
/// </summary>
public interface ISafetyBandReader
{
    /// <summary>
    /// This party's band, or null when there is no record. <strong>Null is not "adult"</strong> — the
    /// caller resolves it to the strictest band, exactly as an unattested birth date does.
    /// </summary>
    Task<string?> GetSafetyBandAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}
