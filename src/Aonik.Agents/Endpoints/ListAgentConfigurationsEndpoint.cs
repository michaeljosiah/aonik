using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List agent configurations";
            s.Description = "Returns all agent configurations visible to the current tenant, including global defaults and tenant-specific overrides.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
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
