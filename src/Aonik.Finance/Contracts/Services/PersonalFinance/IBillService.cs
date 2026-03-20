using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IBillService
{
    Task<BillResponse> CreateBillAsync(
        CreateBillRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillResponse>> ListBillsAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<BillResponse?> GetBillAsync(
        Guid billId,
        CancellationToken cancellationToken = default);

    Task<BillResponse> UpdateBillAsync(
        Guid billId,
        UpdateBillRequest request,
        CancellationToken cancellationToken = default);

    Task ArchiveBillAsync(
        Guid billId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillResponse>> GetUpcomingBillsAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default);
}
