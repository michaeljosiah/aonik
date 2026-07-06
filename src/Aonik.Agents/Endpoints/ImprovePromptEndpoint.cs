using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Uses AI to refine an agent's system prompt based on user instructions.
/// The AI preserves the core theme and intent of the existing prompt while
/// incorporating the requested changes.
///
/// The model is resolved via <see cref="IAiTaskProfileResolver"/> using the
/// "prompt-improvement" use-case key, following the pattern established by
/// <see cref="Aonik.Agents.Framework.ChatThreadTitleGenerator"/>. Falls back to
/// <see cref="DefaultModelId"/> when no route policy is configured.
/// </summary>
internal sealed class ImprovePromptEndpoint
    : Endpoint<ImprovePromptRequest, ImprovePromptResponse>
{
    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;
    private const string DefaultModelId = "gpt-5-mini";
    private const string PromptImprovementUseCase = "prompt-improvement";

    public ImprovePromptEndpoint(IChatClient chatClient, IAiTaskProfileResolver profileResolver)
    {
        _chatClient = chatClient;
        _profileResolver = profileResolver;
    }

    public override void Configure()
    {
        Post("/ai/agents/improve-prompt");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Improve an agent system prompt";
            s.Description = "Uses AI to refine an agent's system prompt based on user instructions, preserving the core theme and intent.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ImprovePromptRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPrompt) && string.IsNullOrWhiteSpace(req.UserIntent))
        {
            ThrowError("At least one of CurrentPrompt or UserIntent must be provided.");
        }

        var defaultSystemMessage = """
            You are an expert prompt engineer. Your job is to improve system prompts for AI agents.

            Rules:
            - If an existing prompt is provided, preserve its core theme, domain context, and intent.
            - Incorporate the user's requested changes naturally into the prompt.
            - Keep the tone professional and clear.
            - Use structured formatting (numbered lists, sections) when the prompt is complex.
            - If no existing prompt is provided, create a new one based on the user's intent.
            - Output ONLY the improved prompt text — no explanations, no markdown fences, no preamble.
            """;

        var profile = await _profileResolver.ResolveAsync(
            PromptImprovementUseCase, defaultModelId: DefaultModelId, cancellationToken: ct);

        var systemMessage = string.IsNullOrEmpty(profile.SystemPrompt)
            ? defaultSystemMessage
            : profile.SystemPrompt;

        var userMessage = string.IsNullOrWhiteSpace(req.CurrentPrompt)
            ? $"Create a system prompt for an AI agent based on this intent:\n\n{req.UserIntent}"
            : $"Here is the current system prompt:\n\n---\n{req.CurrentPrompt}\n---\n\nPlease improve it based on this guidance: {req.UserIntent}";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, userMessage),
        };

        // Stamp the use_case so the AiTraceObservation row carries a semantic
        // trace name ("prompt-improvement") instead of leaking the model id
        // (mirrors ChatThreadTitleGenerator's telemetry convention).
        var options = new ChatOptions { ModelId = profile.ModelId ?? DefaultModelId };
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties[AiTelemetry.UseCaseAttribute] = PromptImprovementUseCase;

        var response = await _chatClient.GetResponseAsync(
            messages,
            options: options,
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
