using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Services;

/// <summary>
/// Persists AI-generated insights to the AI module's database.
/// Implements the SharedKernel contract so domain modules can save insights
/// without depending on AiDbContext directly.
/// </summary>
internal sealed class InsightWriter : IInsightWriter
{
    private readonly AiDbContext _dbContext;

    public InsightWriter(AiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InsightResponse> SaveInsightAsync(
        string subjectType,
        Guid subjectId,
        string title,
        string summary,
        string? metadataJson = null,
        Guid? userId = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var insight = new Insight
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Title = title,
            Summary = summary,
            MetadataJson = metadataJson,
            ExpiresAt = expiresAt,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.Insights.Add(insight);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InsightResponse(
            insight.Id,
            insight.SubjectType,
            insight.SubjectId,
            insight.Title,
            insight.Summary,
            insight.CreatedUtc);
    }
}
