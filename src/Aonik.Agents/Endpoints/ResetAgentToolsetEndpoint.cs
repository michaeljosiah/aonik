using Aonik.Agents.Contracts.Models;
using Aonik.SharedKernel.Abstractions.Agents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Resets the agent's tool catalogue (<c>ToolsetIdsJson</c>) to the live list
/// returned by the agent's <c>IDomainAgentDescriptor.GetToolNames(sp)</c>.
/// Useful when the in-code tool surface has changed (e.g. a sub-agent trigger
/// was renamed or added by a release) but the persisted Agent row still
/// reflects the pre-release whitelist — <c>SeedGlobalDefaultsAsync</c> is
/// deliberately idempotent for the toolset to preserve admin customisations,
/// so this endpoint is the explicit opt-in path to refresh from code.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ResetAgentPromptEndpoint"/>'s shape. Only the toolset is
/// overwritten; prompt, model, risk tier, and other customisations are
/// preserved. Targets the tenant override if one exists for the current
/// tenant, otherwise the global row.
/// </remarks>
internal sealed class ResetAgentToolsetEndpoint
    : Endpoint<ResetAgentToolsetRequest, AgentConfigurationResponse>
{
    private readonly IAgentConfigurationService _configService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResetAgentToolsetEndpoint(
        IAgentConfigurationService configService,
        IHttpContextAccessor httpContextAccessor)
    {
        _configService = configService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override void Configure()
    {
        Post("/ai/agents/configurations/{Name}/reset-toolset");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Reset agent toolset to descriptor default";
            s.Description = "Overwrites the agent's ToolsetIdsJson with the live tool list from its IDomainAgentDescriptor.GetToolNames(sp). Other customisations are preserved.";
            s.Response(200, "Toolset reset");
            s.Response(401, "Not authenticated");
            s.Response(404, "Agent or descriptor not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ResetAgentToolsetRequest req, CancellationToken ct)
    {
        // The descriptor's GetToolNames builds AIFunction instances from
        // DI-resolved services, so we pass the per-request scoped
        // IServiceProvider rather than the root.
        var serviceProvider = _httpContextAccessor.HttpContext?.RequestServices
            ?? throw new InvalidOperationException(
                "Request scope is required to reset an agent toolset.");

        var result = await _configService.ResetToolsetAsync(req.Name, serviceProvider, ct);
        await Send.OkAsync(result, ct);
    }
}

public sealed record ResetAgentToolsetRequest
{
    public string Name { get; init; } = string.Empty;
}
