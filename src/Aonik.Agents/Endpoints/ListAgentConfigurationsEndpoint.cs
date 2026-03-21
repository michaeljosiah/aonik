using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists all agent configurations visible to the current tenant.
/// Returns both global defaults and any tenant-specific overrides.
/// </summary>
internal sealed class ListAgentConfigurationsEndpoint
    : EndpointWithoutRequest<ListAgentConfigurationsResponse>
{
    private readonly IAgentConfigurationService _configService;

    public ListAgentConfigurationsEndpoint(IAgentConfigurationService configService)
    {
        _configService = configService;
    }

    public override void Configure()
    {
        Get("/ai/agents/configurations");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var configs = await _configService.ListAsync(ct);
        await Send.OkAsync(new ListAgentConfigurationsResponse { Configurations = configs }, ct);
    }
}

public sealed record ListAgentConfigurationsResponse
{
    public required IReadOnlyList<AgentConfigurationResponse> Configurations { get; init; }
}
