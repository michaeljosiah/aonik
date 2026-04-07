using System.Text.Json;
using System.Text.RegularExpressions;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AI wizard endpoint that generates a playground scenario from natural language instructions.
/// Uses the <c>playground_scenario_generation</c> AiTask for its system/user prompts.
/// Returns the generated scenario as JSON (not persisted — caller saves separately).
/// </summary>
public static class PlaygroundScenarioGenerateEndpoint
{
    private const string GeneratorUseCase = "playground_scenario_generation";
    private const string GeneratorPromptName = "playground_scenario_generation";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Maps the scenario generation endpoint.
    /// </summary>
    public static IEndpointConventionBuilder MapPlaygroundScenarioGenerate(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/playground/scenarios/generate")
    {
        return endpoints.MapPost(pattern, HandleGenerateRequest)
            .WithName("GeneratePlaygroundScenario")
            .WithTags("AI Playground")
            .WithSummary("Generate a playground scenario from natural language instructions using AI");
    }

    private static async Task<IResult> HandleGenerateRequest(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aonik.Agents.PlaygroundScenarioGenerate");

        // ── Parse request ───────────────────────────────────────────────
        GeneratePlaygroundScenarioRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<GeneratePlaygroundScenarioRequest>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "ScenarioGenerate: invalid request body");
            return Results.BadRequest(new { message = "Invalid request body" });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Instructions))
            return Results.BadRequest(new { message = "'instructions' is required" });

        try
        {
            // ── Resolve generator prompt from AiTask ────────────────────
            var profileResolver = context.RequestServices.GetRequiredService<IAiTaskProfileResolver>();
            var profile = await profileResolver.ResolveAsync(
                GeneratorUseCase, GeneratorPromptName, cancellationToken: cancellationToken);

            var systemPrompt = profile.SystemPrompt ?? FallbackSystemPrompt;
            var userTemplate = profile.UserPromptTemplate ?? FallbackUserTemplate;

            // ── Build user prompt ───────────────────────────────────────
            var userPrompt = ApplyVariables(userTemplate, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["INSTRUCTIONS"] = request.Instructions,
                ["AGENT_NAME"] = request.AgentName ?? "(not specified)",
                ["AI_TASK_ID"] = request.AiTaskId?.ToString() ?? "(not specified)",
            });

            // ── Optionally enrich with agent context ────────────────────
            if (!string.IsNullOrWhiteSpace(request.AgentName))
            {
                var agentConfigService = context.RequestServices.GetRequiredService<IAgentConfigurationService>();
                var agentConfig = await agentConfigService.GetResolvedAsync(request.AgentName, cancellationToken);
                if (agentConfig is not null)
                {
                    userPrompt += $"\n\n## Agent Context\n- Agent: {agentConfig.Name}\n- Description: {agentConfig.Description}\n- Tools: {agentConfig.ToolsetIdsJson}";
                }
            }

            logger.LogInformation(
                "ScenarioGenerate: using {Source} prompt (model: {Model})",
                profile.SystemPrompt is not null ? "AiTask" : "fallback",
                profile.ModelId ?? "default");

            // ── Resolve model ───────────────────────────────────────────
            var chatClient = context.RequestServices.GetRequiredService<IChatClient>();
            ChatOptions? chatOptions = null;

            if (request.ModelId.HasValue)
            {
                var resolver = context.RequestServices.GetRequiredService<IAiModelResolver>();
                var modelName = await resolver.ResolveModelNameByIdAsync(
                    request.ModelId.Value, cancellationToken);
                if (modelName is not null)
                    chatOptions = new ChatOptions { ModelId = modelName };
            }
            else if (profile.ModelId is not null)
            {
                chatOptions = new ChatOptions { ModelId = profile.ModelId };
            }

            // ── Call LLM ────────────────────────────────────────────────
            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt),
            };

            var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken);
            var responseText = response.Text ?? string.Empty;

            // ── Parse structured output ─────────────────────────────────
            var parsed = TryParseScenarioJson(responseText);
            if (parsed is null)
            {
                logger.LogWarning("ScenarioGenerate: failed to parse LLM response as scenario JSON");
                return Results.UnprocessableEntity(new
                {
                    message = "AI generated a response but it could not be parsed as a valid scenario",
                    rawText = responseText,
                });
            }

            // Apply agent/task context from request
            if (request.AgentName is not null && parsed.AgentName is null)
                parsed = parsed with { AgentName = request.AgentName };
            if (request.AiTaskId.HasValue && !parsed.AiTaskId.HasValue)
                parsed = parsed with { AiTaskId = request.AiTaskId };

            return Results.Ok(parsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ScenarioGenerate: error generating scenario");
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "Scenario generation failed");
        }
    }

    /// <summary>
    /// Attempts to parse the LLM response as a scenario.
    /// Handles markdown code fences and bare JSON.
    /// </summary>
    private static PlaygroundScenarioResponse? TryParseScenarioJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Try direct parse
        if (TryParse(text, out var result))
            return result;

        // Try extracting from markdown code fences
        var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            var contentStart = text.IndexOf('\n', jsonStart);
            if (contentStart >= 0)
            {
                var jsonEnd = text.IndexOf("```", contentStart, StringComparison.Ordinal);
                if (jsonEnd > contentStart)
                {
                    var json = text[(contentStart + 1)..jsonEnd].Trim();
                    if (TryParse(json, out result))
                        return result;
                }
            }
        }

        // Try extracting first { ... } block
        var braceStart = text.IndexOf('{');
        var braceEnd = text.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            var json = text[braceStart..(braceEnd + 1)];
            if (TryParse(json, out result))
                return result;
        }

        return null;
    }

    private static bool TryParse(string json, out PlaygroundScenarioResponse? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<PlaygroundScenarioResponse>(json, JsonOptions);
            return result is not null && !string.IsNullOrWhiteSpace(result.Name);
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private static string ApplyVariables(string template, Dictionary<string, string> variables)
    {
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    // ── Fallback prompts ───────────────────────────────────────────────────

    private const string FallbackSystemPrompt = """
        <role>
        You are an AI Playground Scenario Generator for the AONIK platform. You create realistic, multi-turn conversation scenarios for testing AI agents and tasks.
        </role>

        <task>
        Generate a complete playground scenario — a structured conversation setup with a name, description, tags, optional system prompt, and a series of user/assistant message turns — based on the user's natural language instructions.
        </task>

        <constraints>
        - Generate realistic, domain-appropriate conversation turns.
        - Include 2-6 turns unless the user requests otherwise.
        - Alternate between "user" and "assistant" roles naturally.
        - The first turn should always be a "user" message.
        - Tags should be lowercase, hyphenated, and relevant to the scenario content.
        - Do not include actual sensitive data (account numbers, SSNs, etc.) — use realistic placeholders.
        - Assistant turns should reflect how a well-configured agent would respond, including referencing tools and data.
        </constraints>

        <output_contract>
        Return valid JSON only — no markdown fences, no commentary outside the JSON.
        Use this exact structure:
        {
          "name": "<short descriptive name>",
          "description": "<1-2 sentence description of what this scenario tests>",
          "tags": ["<tag1>", "<tag2>"],
          "systemPrompt": "<optional system prompt override, or null>",
          "turns": [
            { "role": "user", "content": "<user message>" },
            { "role": "assistant", "content": "<expected assistant response>" },
            ...
          ]
        }
        </output_contract>

        <definition_of_done>
        The scenario is complete when:
        - name is a clear, concise title (under 100 characters)
        - description explains what the scenario tests
        - tags contains 1-5 relevant lowercase tags
        - turns contains at least 2 messages starting with a user message
        - All turns have valid role ("user" or "assistant") and non-empty content
        - The output is valid, parseable JSON
        </definition_of_done>
        """;

    private const string FallbackUserTemplate = """
        ## Instructions
        {{INSTRUCTIONS}}

        ## Context
        - Target agent: {{AGENT_NAME}}
        - Target AI task: {{AI_TASK_ID}}

        Generate the scenario as JSON.
        """;
}
