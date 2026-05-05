using Aonik.SharedKernel.Abstractions.Agents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get agent configuration by name";
            s.Description = "Returns the resolved agent configuration for a given agent name, preferring tenant-specific overrides over global defaults.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Agent configuration not found");
        });
        Options(x => x.WithTags("AI Agents"));
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
