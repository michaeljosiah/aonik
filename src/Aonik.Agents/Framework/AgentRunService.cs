using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Framework;

/// <summary>
/// Queries agent execution history from the <see cref="AgentsDbContext"/>.
/// </summary>
internal sealed class AgentRunService : IAgentRunService
{
    private readonly AgentsDbContext _dbContext;

    public AgentRunService(AgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AgentRunSummary>> ListByAgentAsync(
        Guid agentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AgentRuns
            .AsNoTracking()
            .Where(r => r.AgentId == agentId);

        var totalCount = await query.CountAsync(cancellationToken);

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = runs.Select(r => new AgentRunSummary
        {
            Id = r.Id,
            AgentId = r.AgentId,
            Goal = r.Goal,
            Status = r.Status,
            StepCount = CountJsonArrayElements(r.StepsJson),
            LinkedAiRunCount = CountJsonArrayElements(r.LinkedAiRunIdsJson),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        }).ToList();

        return new PagedResult<AgentRunSummary>(items, totalCount, page, pageSize);
    }

    private static int CountJsonArrayElements(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch
        {
            return 0;
        }
    }
}
