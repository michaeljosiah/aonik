namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Generic response for AI-generated insights.
/// Used across modules for any insight subject type (Invoice, Order, etc.).
/// </summary>
public record InsightResponse(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    string Title,
    string Summary,
    DateTime CreatedUtc);
