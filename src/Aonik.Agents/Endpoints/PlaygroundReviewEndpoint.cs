using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Aonik.Agents.Contracts.Models;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AI Playground review endpoint. Evaluates a playground conversation using
/// RAGAS-style metrics (Faithfulness, Answer Relevancy, Coherence, Completeness)
/// by making a separate LLM call with a structured reviewer prompt.
///
/// The reviewer prompt is resolved from the <c>playground_response_review</c>
/// AiTask, making it fully configurable from the Admin UI. Falls back to a
/// hardcoded prompt if the task has not been seeded yet.
///
/// Streams the review result via SSE.
/// </summary>
public static class PlaygroundReviewEndpoint
{
    private const string ReviewerUseCase = "playground_response_review";
    private const string ReviewerPromptName = "playground_response_review";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Maps the playground review endpoint at the specified route pattern.
    /// </summary>
    public static IEndpointConventionBuilder MapPlaygroundReview(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/playground/review")
    {
        return endpoints.MapPost(pattern, HandleReviewRequest)
            .WithName("AiPlaygroundReview")
            .WithTags("AI Agents")
            .WithSummary("Review a playground agent conversation")
            .WithDescription("Evaluates an agent's responses using RAGAS-style metrics (Faithfulness, Answer Relevancy, Coherence, Completeness). Streams the review result via SSE. Prompt is configurable via the 'playground_response_review' AI Task.");
    }

    private static async Task HandleReviewRequest(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aonik.Agents.PlaygroundReview");

        // ── Parse request ───────────────────────────────────────────────
        PlaygroundReviewRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PlaygroundReviewRequest>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "PlaygroundReview: invalid request body");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid request body", cancellationToken);
            return;
        }

        if (request is null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Request body is required", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.AssistantResponse))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("'assistantResponse' is required for review", cancellationToken);
            return;
        }

        // ── Set SSE headers ─────────────────────────────────────────────
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var reviewId = Guid.NewGuid().ToString("N");

        try
        {
            await WriteSseEventAsync(context.Response, new
            {
                type = "REVIEW_STARTED",
                reviewId,
            }, cancellationToken);

            // ── Resolve reviewer prompt from AiTask ─────────────────────
            var profileResolver = context.RequestServices.GetRequiredService<IAiTaskProfileResolver>();
            var profile = await profileResolver.ResolveAsync(
                ReviewerUseCase, ReviewerPromptName, cancellationToken: cancellationToken);

            var reviewerSystemPrompt = profile.SystemPrompt ?? FallbackSystemPrompt;
            var reviewerUserTemplate = profile.UserPromptTemplate ?? FallbackUserTemplate;

            logger.LogInformation(
                "PlaygroundReview: using {Source} prompt (model: {Model})",
                profile.SystemPrompt is not null ? "AiTask" : "fallback",
                profile.ModelId ?? "default");

            // ── Build template variables ────────────────────────────────
            var variables = BuildTemplateVariables(request);
            var reviewerUserPrompt = ApplyVariables(reviewerUserTemplate, variables);

            // ── Resolve model ───────────────────────────────────────────
            var chatClient = context.RequestServices.GetRequiredService<IChatClient>();
            ChatOptions? chatOptions = null;

            // Priority: explicit model from request → model from AiTask route policy
            if (request.ModelId.HasValue)
            {
                var resolver = context.RequestServices.GetRequiredService<IAiModelResolver>();
                var modelName = await resolver.ResolveModelNameByIdAsync(
                    request.ModelId.Value, cancellationToken);

                if (modelName is not null)
                {
                    chatOptions = new ChatOptions { ModelId = modelName };
                }
            }
            else if (profile.ModelId is not null)
            {
                chatOptions = new ChatOptions { ModelId = profile.ModelId };
            }

            // ── Stream the review ───────────────────────────────────────
            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, reviewerSystemPrompt),
                new(ChatRole.User, reviewerUserPrompt),
            };

            var responseText = new StringBuilder();

            await foreach (var update in chatClient.GetStreamingResponseAsync(
                chatMessages, chatOptions, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                foreach (var content in update.Contents ?? [])
                {
                    if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        responseText.Append(textContent.Text);

                        await WriteSseEventAsync(context.Response, new
                        {
                            type = "REVIEW_CONTENT",
                            reviewId,
                            delta = textContent.Text,
                        }, cancellationToken);
                    }
                }
            }

            // ── Try to parse structured review from the response ────────
            var fullText = responseText.ToString();
            var parsed = TryParseReviewJson(fullText);

            await WriteSseEventAsync(context.Response, new
            {
                type = "REVIEW_FINISHED",
                reviewId,
                parsed,
                rawText = parsed is null ? fullText : null,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("PlaygroundReview: stream cancelled for review {ReviewId}", reviewId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlaygroundReview: error for review {ReviewId}", reviewId);
            try
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "REVIEW_ERROR",
                    reviewId,
                    message = ex.Message,
                }, CancellationToken.None);
            }
            catch
            {
                // Connection already broken
            }
        }

        await context.Response.Body.FlushAsync(CancellationToken.None);
    }

    // ── Template variable construction ──────────────────────────────────────

    /// <summary>
    /// Builds the template variable dictionary from the review request,
    /// matching the <c>{{VARIABLE}}</c> placeholders in the AiTask templates.
    /// </summary>
    private static Dictionary<string, string> BuildTemplateVariables(PlaygroundReviewRequest request)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // SYSTEM_PROMPT
        vars["SYSTEM_PROMPT"] = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? "(No system prompt provided)"
            : request.SystemPrompt;

        // USER_BRIEF
        if (!string.IsNullOrWhiteSpace(request.UserBriefJson))
        {
            vars["USER_BRIEF"] = $"```json\n{request.UserBriefJson}\n```";
        }
        else
        {
            vars["USER_BRIEF"] = "(No user brief provided)";
        }

        // CONVERSATION
        var conversationSb = new StringBuilder();
        if (request.Messages is { Count: > 0 })
        {
            foreach (var msg in request.Messages)
            {
                conversationSb.AppendLine($"**{msg.Role?.ToUpperInvariant() ?? "USER"}**: {msg.Content}");
                conversationSb.AppendLine();
            }
        }
        else
        {
            conversationSb.AppendLine("(No conversation messages)");
        }
        vars["CONVERSATION"] = conversationSb.ToString().TrimEnd();

        // TOOL_CALLS
        if (request.ToolCalls is { Count: > 0 })
        {
            var toolSb = new StringBuilder();
            foreach (var tc in request.ToolCalls)
            {
                toolSb.AppendLine($"- **{tc.ToolName}**({tc.Arguments})");
                if (!string.IsNullOrWhiteSpace(tc.Result))
                {
                    toolSb.AppendLine($"  Result: {tc.Result}");
                }
                toolSb.AppendLine();
            }
            vars["TOOL_CALLS"] = toolSb.ToString().TrimEnd();
        }
        else
        {
            vars["TOOL_CALLS"] = "(No tool calls)";
        }

        // ASSISTANT_RESPONSE
        vars["ASSISTANT_RESPONSE"] = request.AssistantResponse ?? string.Empty;

        return vars;
    }

    /// <summary>
    /// Replaces <c>{{variableName}}</c> placeholders in a template with
    /// values from the provided dictionary.
    /// </summary>
    private static string ApplyVariables(string template, Dictionary<string, string> variables)
    {
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    // ── JSON extraction ─────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to extract and parse the JSON review from the LLM response text.
    /// Handles cases where the JSON may be wrapped in markdown code fences.
    /// </summary>
    private static JsonElement? TryParseReviewJson(string text)
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

    private static bool TryParse(string json, out JsonElement result)
    {
        try
        {
            result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Writes a single SSE event as a <c>data: {json}\n\n</c> line.
    /// </summary>
    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        T eventData,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(eventData, JsonOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    // ── Fallback prompts (used when AiTask has not been seeded) ─────────────

    private const string FallbackSystemPrompt = """
        <role>
        You are a rigorous AI response quality evaluator specialising in RAGAS-style metrics for conversational AI agents on the AONIK platform.
        </role>

        <task>
        Evaluate the assistant response provided in the context against four quality metrics — Faithfulness, Answer Relevancy, Coherence, and Completeness — each scored 1-5. Produce a structured quality report with scores, explanations, strengths, improvement suggestions, and concrete prompt rewrites.
        </task>

        <context>
        The user will provide:
        - The agent's system prompt (its instructions)
        - Optional user brief (contextual JSON data injected at runtime)
        - The conversation messages (user and assistant turns)
        - Any tool calls the agent made and their results
        - The final assistant response to evaluate
        </context>

        <constraints>
        - Score each metric independently on a 1-5 integer scale using ONLY the evidence available in the provided context.
        - Do not infer external knowledge the agent could not have had access to.
        - A claim is "faithful" only if it is directly supported by the system prompt, user brief, tool results, or conversation history.
        - If the agent had no tools or user brief, evaluate faithfulness against the system prompt and user query alone.
        - Each metric explanation must be 2-3 sentences with specific references to the response content.
        - Strengths, suggestions, and prompt improvements must each contain at least 1 and at most 5 items.
        - Prompt improvements must be concrete, copy-pasteable additions or rewrites to the system prompt — not vague advice.
        </constraints>

        <output_contract>
        - Return valid JSON only — no markdown fences, no commentary outside the JSON.
        - Use this exact structure:
        {
          "overallScore": <number 1-5, weighted average of the four metrics>,
          "metrics": [
            {
              "name": "Faithfulness",
              "score": <1-5>,
              "explanation": "<2-3 sentences. 5=all claims grounded in context, 3=minor unsupported claims, 1=significant hallucination>"
            },
            {
              "name": "Answer Relevancy",
              "score": <1-5>,
              "explanation": "<2-3 sentences. 5=directly addresses query, 3=partially relevant, 1=off-topic>"
            },
            {
              "name": "Coherence",
              "score": <1-5>,
              "explanation": "<2-3 sentences. 5=excellent structure and flow, 3=some disorganisation, 1=confusing or contradictory>"
            },
            {
              "name": "Completeness",
              "score": <1-5>,
              "explanation": "<2-3 sentences. 5=comprehensive, 3=main points covered with gaps, 1=severely incomplete>"
            }
          ],
          "strengths": ["<specific strength referencing response content>"],
          "suggestions": ["<actionable suggestion to improve the response>"],
          "promptImprovements": ["<concrete system prompt addition or rewrite, copy-pasteable>"]
        }
        </output_contract>

        <definition_of_done>
        The evaluation is complete only when:
        - All four metrics have an integer score between 1 and 5 with a 2-3 sentence explanation.
        - overallScore is the weighted average of the four metric scores.
        - strengths contains at least 1 specific positive observation referencing the response.
        - suggestions contains at least 1 actionable improvement for the response.
        - promptImprovements contains at least 1 concrete, copy-pasteable system prompt change.
        - The output is valid, parseable JSON with no text outside the JSON object.
        </definition_of_done>
        """;

    private const string FallbackUserTemplate = """
        ## Agent System Prompt
        {{SYSTEM_PROMPT}}

        ## User Brief Context
        {{USER_BRIEF}}

        ## Conversation Messages
        {{CONVERSATION}}

        ## Tool Calls Made
        {{TOOL_CALLS}}

        ## Assistant Response to Review
        {{ASSISTANT_RESPONSE}}

        Please evaluate this response and provide your assessment as JSON.
        """;
}
