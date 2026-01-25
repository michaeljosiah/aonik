using Aonik.Application.Models.Compliance;

namespace Aonik.Application.Services.Compliance;

public interface IComplianceService
{
    Task<ScreeningResult> ScreenPartyAsync(
        Guid partyId,
        string checkType,
        CancellationToken cancellationToken = default);

    Task<ComplianceCaseResponse> CreateOrderReviewCaseAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<bool> RequiresComplianceReviewAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
