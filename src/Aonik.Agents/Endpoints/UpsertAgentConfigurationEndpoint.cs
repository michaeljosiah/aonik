using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Creates or updates a tenant-level agent configuration override for the given agent name.
/// Fields not provided in the request body preserve their existing or global default values.
/// </summary>
internal sealed class UpsertAgentConfigurationEndpoint
    : Endpoint<UpsertAgentConfigurationEndpointRequest, AgentConfigurationResponse>
{
    private readonly IAgentConfigurationService _configService;

    public UpsertAgentConfigurationEndpoint(IAgentConfigurationService configService)
    {
        _configService = configService;
    }

    public override void Configure()
    {
        Put("/ai/agents/configurations/{Name}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpsertAgentConfigurationEndpointRequest req, CancellationToken ct)
    {
        var serviceRequest = new UpsertAgentConfigurationRequest
        {
            Description = req.Description,
            InstructionsText = req.InstructionsText,
            ToolsetIdsJson = req.ToolsetIdsJson,
            PermissionsProfileJson = req.PermissionsProfileJson,
            RiskTier = req.RiskTier,
            IsActive = req.IsActive,
            ModelId = req.ModelId,
            IconUrl = req.IconUrl
        };

        var result = await _configService.UpsertOverrideAsync(req.Name, serviceRequest, ct);
        await Send.OkAsync(result, ct);
    }
}

/// <summary>
/// Endpoint request that combines the route parameter (Name) with the body fields.
/// </summary>
public sealed record UpsertAgentConfigurationEndpointRequest
{
    /// <summary>Agent name from the route.</summary>
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
    public string? InstructionsText { get; init; }
    public string? ToolsetIdsJson { get; init; }
    public string? PermissionsProfileJson { get; init; }
    public string? RiskTier { get; init; }
    public bool? IsActive { get; init; }
    public Guid? ModelId { get; init; }
    public string? IconUrl { get; init; }
}
