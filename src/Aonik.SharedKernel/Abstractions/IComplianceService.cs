namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module compliance service contract. Implemented by Platform, consumed by Finance.
/// </summary>
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

public record ScreeningResult(
    Guid ScreeningCheckId,
    Guid PartyId,
    string CheckType,
    string ResultStatus,
    string? Decision,
    DateTime? DecidedAt);

public record ComplianceCaseResponse(
    Guid ComplianceCaseId,
    string CaseType,
    string Status,
    Guid? LinkedOrderId,
    Guid? LinkedPartyId);
