namespace Aonik.Application.Models.Compliance;

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
