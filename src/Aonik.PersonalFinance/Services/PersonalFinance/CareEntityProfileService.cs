using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CareEntityProfileService : ICareEntityProfileService
{
    private readonly ICareEntityService _careEntityService;

    public CareEntityProfileService(ICareEntityService careEntityService)
        => _careEntityService = careEntityService;

    public async Task<CareEntityProfileResponse?> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _careEntityService.GetAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // The dependent aggregates attach as their specs land (§8):
        //   YearTotals + RecentLogs ← PaymentLog            (Spec 045)
        //   Commitments             ← Commitment.CareEntityId (Spec 044)
        //   Documents               ← DocumentLink            (Spec 046)
        // Until then the profile is the entity with empty dependent arrays —
        // one round-trip that "grows richer as 044–046 land".
        return new CareEntityProfileResponse(
            Entity: entity,
            YearTotals: [],
            Commitments: [],
            RecentLogs: [],
            Documents: []);
    }
}
