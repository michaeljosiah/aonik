using FastEndpoints;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Uses AI to refine an agent's system prompt based on user instructions.
/// The AI preserves the core theme and intent of the existing prompt while
/// incorporating the requested changes.
/// </summary>
internal sealed class ImprovePromptEndpoint
    : Endpoint<ImprovePromptRequest, ImprovePromptResponse>
{
    private readonly IChatClient _chatClient;

    public ImprovePromptEndpoint(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public override void Configure()
    {
        Post("/ai/agents/improve-prompt");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ImprovePromptRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPrompt) && string.IsNullOrWhiteSpace(req.UserIntent))
        {
            ThrowError("At least one of CurrentPrompt or UserIntent must be provided.");
        }

        var systemMessage = """
            You are an expert prompt engineer. Your job is to improve system prompts for AI agents.

            Rules:
            - If an existing prompt is provided, preserve its core theme, domain context, and intent.
            - Incorporate the user's requested changes naturally into the prompt.
            - Keep the tone professional and clear.
            - Use structured formatting (numbered lists, sections) when the prompt is complex.
            - If no existing prompt is provided, create a new one based on the user's intent.
            - Output ONLY the improved prompt text — no explanations, no markdown fences, no preamble.
            """;

        var userMessage = string.IsNullOrWhiteSpace(req.CurrentPrompt)
            ? $"Create a system prompt for an AI agent based on this intent:\n\n{req.UserIntent}"
            : $"Here is the current system prompt:\n\n---\n{req.CurrentPrompt}\n---\n\nPlease improve it based on this guidance: {req.UserIntent}";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, userMessage),
        };

        var response = await _chatClient.GetResponseAsync(
            messages,
            options: new ChatOptions { ModelId = "gpt-5-mini" },
            cancellationToken: ct);

        var improvedPrompt = response.Text?.Trim() ?? req.CurrentPrompt ?? string.Empty;

        await Send.OkAsync(new ImprovePromptResponse(improvedPrompt), ct);
    }
}

public sealed record ImprovePromptRequest(
    string? CurrentPrompt,
    string UserIntent);

public sealed record ImprovePromptResponse(
    string ImprovedPrompt);
