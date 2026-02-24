using Aonik.Agents.Framework;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists all registered domain agents available to the orchestrator.
/// Useful for the Admin UI to show which agents are active.
/// </summary>
internal sealed class ListAgentsEndpoint : EndpointWithoutRequest<ListAgentsResponse>
{
    private readonly IEnumerable<AonikDomainAgent> _agents;

    public ListAgentsEndpoint(IEnumerable<AonikDomainAgent> agents)
    {
        _agents = agents;
    }

    public override void Configure()
    {
        Get("/ai/agents");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var agents = _agents.Select(a => new AgentInfo
        {
            Name = a.Name,
            Description = a.Description
        }).ToList();

        await Send.OkAsync(new ListAgentsResponse { Agents = agents }, ct);
    }
}

public sealed record ListAgentsResponse
{
    public required List<AgentInfo> Agents { get; init; }
}

public sealed record AgentInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}
