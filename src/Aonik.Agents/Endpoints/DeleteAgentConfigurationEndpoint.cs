using Aonik.SharedKernel.Abstractions.Agents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Deletes the tenant-specific agent configuration override for the given agent name,
/// reverting the tenant to the global default configuration.
/// </summary>
internal sealed class DeleteAgentConfigurationEndpoint
    : Endpoint<DeleteAgentConfigurationRequest>
{
    private readonly IAgentConfigurationService _configService;

    public DeleteAgentConfigurationEndpoint(IAgentConfigurationService configService)
    {
        _configService = configService;
    }

    public override void Configure()
    {
        Delete("/ai/agents/configurations/{Name}");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Delete agent configuration override";
            s.Description = "Deletes the tenant-specific agent configuration override, reverting the tenant to the global default.";
            s.Response(204, "Configuration deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Agent configuration not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(DeleteAgentConfigurationRequest req, CancellationToken ct)
    {
        await _configService.DeleteOverrideAsync(req.Name, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeleteAgentConfigurationRequest
{
    public string Name { get; init; } = string.Empty;
}
