using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Gets the resolved agent configuration for a given agent name.
/// Returns the tenant-specific override if it exists, otherwise the global default.
/// </summary>
internal sealed class GetAgentConfigurationEndpoint
    : Endpoint<GetAgentConfigurationRequest, AgentConfigurationResponse>
{
    private readonly IAgentConfigurationService _configService;

    public GetAgentConfigurationEndpoint(IAgentConfigurationService configService)
    {
        _configService = configService;
    }

    public override void Configure()
    {
        Get("/ai/agents/configurations/{Name}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(GetAgentConfigurationRequest req, CancellationToken ct)
    {
        var config = await _configService.GetResolvedAsync(req.Name, ct);

        if (config is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(config, ct);
    }
}

public sealed record GetAgentConfigurationRequest
{
    public string Name { get; init; } = string.Empty;
}
