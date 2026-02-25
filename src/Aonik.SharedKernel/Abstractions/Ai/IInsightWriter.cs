namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Contract for persisting AI-generated insights.
/// Implemented by the AI module (writes to AiDbContext).
/// Consumed by domain modules that generate insights via AI workflows.
/// </summary>
public interface IInsightWriter
{
    Task<InsightResponse> SaveInsightAsync(
        string subjectType,
        Guid subjectId,
        string title,
        string summary,
        CancellationToken cancellationToken = default);
}
