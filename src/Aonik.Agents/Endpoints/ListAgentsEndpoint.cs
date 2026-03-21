using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists all registered domain agents available to the orchestrator.
/// Useful for the Admin UI to show which agents are active.
/// </summary>
internal sealed class ListAgentsEndpoint : EndpointWithoutRequest<ListAgentsResponse>
{
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;

    public ListAgentsEndpoint(IEnumerable<IDomainAgentDescriptor> descriptors)
    {
        _descriptors = descriptors;
    }

    public override void Configure()
    {
        Get("/ai/agents");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var agents = _descriptors.Select(d => new AgentInfo
        {
            Name = d.Name,
            Description = d.Description
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
