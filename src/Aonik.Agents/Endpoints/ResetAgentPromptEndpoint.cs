using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Resets the agent's instructions (prompt) back to the hard-coded default
/// defined by its <c>IDomainAgentDescriptor</c> in the solution. Only the prompt
/// field is overwritten; other customizations (tools, model, risk tier) are preserved.
/// </summary>
internal sealed class ResetAgentPromptEndpoint
    : Endpoint<ResetAgentPromptRequest, AgentConfigurationResponse>
{
    private readonly IAgentConfigurationService _configService;

    public ResetAgentPromptEndpoint(IAgentConfigurationService configService)
    {
        _configService = configService;
    }

    public override void Configure()
    {
        Post("/ai/agents/configurations/{Name}/reset-prompt");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Reset agent prompt to hard-coded default";
            s.Description = "Overwrites the agent's InstructionsText with the default from its IDomainAgentDescriptor. Other customizations are preserved.";
            s.Response(200, "Prompt reset");
            s.Response(401, "Not authenticated");
            s.Response(404, "Agent or descriptor not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ResetAgentPromptRequest req, CancellationToken ct)
    {
        var result = await _configService.ResetPromptAsync(req.Name, ct);
        await Send.OkAsync(result, ct);
    }
}

public sealed record ResetAgentPromptRequest
{
    public string Name { get; init; } = string.Empty;
}
