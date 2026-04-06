using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Endpoints;

// ── List AI Runs ───────────────────────────────────────────────────

internal sealed class ListAiRunsEndpoint : EndpointWithoutRequest<ListAiRunsResponse>
{
    private readonly AiDbContext _dbContext;

    public ListAiRunsEndpoint(AiDbContext dbContext) => _dbContext = dbContext;

    public override void Configure()
    {
        Get("/ai/runs");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List AI runs";
            s.Description = "Returns paginated AI execution runs filtered by use case, with model name resolution.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var useCase = Query<string>("useCase", isRequired: true);
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.AiRuns
            .Where(r => r.UseCase == useCase)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var runs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Resolve model names
        var modelIds = runs
            .Select(r => r.AiModelId)
            .Distinct()
            .ToList();

        var models = await _dbContext.AiModels
            .Where(m => modelIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.ModelName, ct);

        var items = runs.Select(r => new AiRunSummaryResponse
        {
            Id = r.Id,
            UseCase = r.UseCase,
            ModelName = models.GetValueOrDefault(r.AiModelId),
            TokensUsed = r.TokensUsed,
            CostEstimate = r.CostEstimate,
            LatencyMs = r.LatencyMs,
            Outcome = r.Outcome,
            CreatedAt = r.CreatedAt,
        }).ToList();

        await Send.OkAsync(new ListAiRunsResponse(items, totalCount, page, pageSize), ct);
    }
}

public sealed record ListAiRunsResponse(
    IReadOnlyList<AiRunSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
