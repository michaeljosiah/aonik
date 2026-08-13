using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Reads the band <c>AgeTransitionService</c> maintains from stored exact dates (Spec 095 §10).
///
/// <para>
/// Deliberately a read of persisted state rather than a calculation. The band changes on a date the
/// transition job already computes and stores, and recomputing it here would give two answers that
/// drift — one of them being used to decide what a child sees.
/// </para>
/// </summary>
internal sealed class SafetyBandReader : ISafetyBandReader
{
    private readonly PlatformDbContext _dbContext;

    public SafetyBandReader(PlatformDbContext dbContext) => _dbContext = dbContext;

    public async Task<string?> GetSafetyBandAsync(
        Guid partyId, CancellationToken cancellationToken = default)
    {
        var band = await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => p.SafetyBand)
            .FirstOrDefaultAsync(cancellationToken);

        // Null for both "no such party" and "party with no band". The caller resolves either to the
        // strictest band, which is the same wrong-way default as an unattested birth date.
        return string.IsNullOrWhiteSpace(band) ? null : band;
    }
}
