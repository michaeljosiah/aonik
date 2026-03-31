namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Contract for querying AI-generated insights.
/// Implemented by the AI module (reads from AiDbContext).
/// Consumed by admin endpoints that display insights for a subject.
/// </summary>
public interface IInsightReader
{
    Task<IReadOnlyList<InsightResponse>> ListBySubjectAsync(
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
