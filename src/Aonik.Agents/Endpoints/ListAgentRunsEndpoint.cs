using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists agent runs for a specific agent, ordered by most recent first.
/// </summary>
internal sealed class ListAgentRunsEndpoint
    : Endpoint<ListAgentRunsRequest, PagedResult<AgentRunSummary>>
{
    private readonly IAgentRunService _runService;

    public ListAgentRunsEndpoint(IAgentRunService runService)
    {
        _runService = runService;
    }

    public override void Configure()
    {
        Get("/ai/agents/{AgentId}/runs");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ListAgentRunsRequest req, CancellationToken ct)
    {
        var page = req.Page > 0 ? req.Page : 1;
        var pageSize = req.PageSize is > 0 and <= 100 ? req.PageSize : 20;

        var result = await _runService.ListByAgentAsync(req.AgentId, page, pageSize, ct);
        await Send.OkAsync(result, ct);
    }
}

public sealed record ListAgentRunsRequest
{
    public Guid AgentId { get; init; }

    [QueryParam]
    public int Page { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 20;
}
