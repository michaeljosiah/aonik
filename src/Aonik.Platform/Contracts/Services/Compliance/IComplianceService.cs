using Aonik.Platform.Contracts.Models.Compliance;

namespace Aonik.Platform.Contracts.Services.Compliance;

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
