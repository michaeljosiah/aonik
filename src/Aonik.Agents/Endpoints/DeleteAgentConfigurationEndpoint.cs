using Aonik.Agents.Contracts.Services;
using FastEndpoints;

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
        Policies("AdminUserPolicy");
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
