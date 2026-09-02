using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists all registered domain agents available to the orchestrator.
/// Useful for the Admin UI to show which agents are active.
/// </summary>
internal sealed class ListAgentsEndpoint : EndpointWithoutRequest<ListAgentsResponse>
{
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly DescriptorModuleFilter _moduleFilter;

    public ListAgentsEndpoint(IEnumerable<IDomainAgentDescriptor> descriptors, DescriptorModuleFilter moduleFilter)
    {
        _descriptors = descriptors;
        _moduleFilter = moduleFilter;
    }

    public override void Configure()
    {
        Get("/ai/agents");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List registered domain agents";
            s.Description = "Returns all domain agents registered with the orchestrator, including their names and descriptions.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Spec 097 §12.1: agents from modules disabled for this tenant are not listed.
        var visible = await _moduleFilter.FilterAsync(_descriptors, ct);

        var agents = visible.Select(d => new AgentInfo
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
