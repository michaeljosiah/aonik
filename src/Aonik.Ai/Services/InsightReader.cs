using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class InsightReader : IInsightReader
{
    private readonly AiDbContext _dbContext;

    public InsightReader(AiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InsightResponse>> ListBySubjectAsync(
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Insights
            .AsNoTracking()
            .Where(i => i.SubjectType == subjectType && i.SubjectId == subjectId)
            .OrderByDescending(i => i.CreatedUtc)
            .Select(i => new InsightResponse(
                i.Id,
                i.SubjectType,
                i.SubjectId,
                i.Title,
                i.Summary,
                i.CreatedUtc))
            .ToListAsync(cancellationToken);
    }
}
