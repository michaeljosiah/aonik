using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class CareEntityProfileService : ICareEntityProfileService
{
    private const int RecentLogCount = 10;

    private readonly ICareEntityService _careEntityService;
    private readonly IPaymentLogService _paymentLogService;

    public CareEntityProfileService(
        ICareEntityService careEntityService,
        IPaymentLogService paymentLogService)
    {
        _careEntityService = careEntityService;
        _paymentLogService = paymentLogService;
    }

    public async Task<CareEntityProfileResponse?> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _careEntityService.GetAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // YearTotals + RecentLogs come from PaymentLog (Spec 045 — now wired);
        // both are empty until the entity has logged acts. Commitments (Spec 044)
        // and Documents (Spec 046) attach as those specs land. One round-trip.
        var yearTotals = await _paymentLogService.GetEntityYearTotalsAsync(id, year: null, cancellationToken);
        var recentLogs = await _paymentLogService.GetRecentForEntityAsync(id, RecentLogCount, cancellationToken);

        return new CareEntityProfileResponse(
            Entity: entity,
            YearTotals: yearTotals,
            Commitments: [],
            RecentLogs: recentLogs,
            Documents: []);
    }
}
