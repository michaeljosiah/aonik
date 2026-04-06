using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Chat endpoint that routes user messages through the master orchestrator agent.
/// The orchestrator uses the agent-as-tool pattern to delegate to domain-specific
/// agents (finance, platform, etc.) based on user intent.
/// </summary>
internal sealed class ChatEndpoint : Endpoint<ChatRequest, AgentChatResponse>
{
    private readonly IMasterOrchestratorService _orchestrator;

    public ChatEndpoint(IMasterOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public override void Configure()
    {
        Post("/ai/chat");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Send a chat message";
            s.Description = "Routes a user message through the master orchestrator agent, which delegates to domain-specific agents based on intent.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
    {
        var response = await _orchestrator.ChatAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
