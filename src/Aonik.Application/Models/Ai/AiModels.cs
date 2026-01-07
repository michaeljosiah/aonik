namespace Aonik.Application.Models.Ai;

public record InsightResponse(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    string Title,
    string Summary,
    DateTime CreatedUtc);
