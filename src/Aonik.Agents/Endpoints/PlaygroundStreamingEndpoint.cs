using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AI Playground streaming endpoint. A lightweight SSE endpoint for testing
/// agents, prompts, and models from the Admin UI. Unlike the production
/// <see cref="AguiStreamingEndpoint"/>, this endpoint:
/// <list type="bullet">
///   <item>Accepts an explicit system prompt, model ID, user brief, and tool filter</item>
///   <item>Skips thread persistence (playground runs are ephemeral)</item>
///   <item>Returns token usage and latency metrics in the <c>RUN_FINISHED</c> event</item>
/// </list>
/// </summary>
public static class PlaygroundStreamingEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Maps the playground streaming endpoint at the specified route pattern.
    /// </summary>
    public static IEndpointConventionBuilder MapPlaygroundStreaming(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/playground/run")
    {
        return endpoints.MapPost(pattern, HandlePlaygroundRequest)
            .WithName("AiPlaygroundRun")
            .WithTags("AI", "Playground");
    }

    private static async Task HandlePlaygroundRequest(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aonik.Agents.Playground");

        // ── Parse request ───────────────────────────────────────────────
        PlaygroundRunRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PlaygroundRunRequest>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Playground: invalid request body");
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

        // Validate: must have either an agent name or a system prompt
        if (string.IsNullOrWhiteSpace(request.AgentName) && string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(
                "Either 'agentName' or 'systemPrompt' is required", cancellationToken);
            return;
        }

        var runId = Guid.NewGuid().ToString("N");

        // ── Set SSE headers ─────────────────────────────────────────────
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // ── Build the agent ─────────────────────────────────────────────
        AIAgent agent;
        try
        {
            agent = await BuildAgentAsync(request, context.RequestServices, logger, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Playground: agent build failed");
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_ERROR",
                message = ex.Message,
                code = "AGENT_BUILD_FAILED",
            }, cancellationToken);
            return;
        }

        // ── Build ChatOptions for model/temperature/maxTokens ───────────
        var chatOptions = await BuildChatOptionsAsync(
            request, context.RequestServices, logger, cancellationToken);

        // ── Assemble messages ───────────────────────────────────────────
        var chatMessages = BuildChatMessages(request);

        // ── Stream ──────────────────────────────────────────────────────
        var stopwatch = Stopwatch.StartNew();
        var assistantText = new StringBuilder();
        long inputTokens = 0;
        long outputTokens = 0;

        try
        {
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_STARTED",
                runId,
            }, cancellationToken);

            var messageId = Guid.NewGuid().ToString("N");
            var messageStarted = false;

            ChatClientAgentRunOptions? runOptions = chatOptions is not null
                ? new ChatClientAgentRunOptions { ChatOptions = chatOptions }
                : null;

            await foreach (var update in agent.RunStreamingAsync(
                chatMessages, session: null, options: runOptions, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var chatUpdate = update.AsChatResponseUpdate();
                if (chatUpdate is null) continue;

                foreach (var content in chatUpdate.Contents ?? [])
                {
                    switch (content)
                    {
                        case TextContent textContent when !string.IsNullOrEmpty(textContent.Text):
                            if (!messageStarted)
                            {
                                await WriteSseEventAsync(context.Response, new
                                {
                                    type = "TEXT_MESSAGE_START",
                                    messageId,
                                    role = "assistant",
                                }, cancellationToken);
                                messageStarted = true;
                            }

                            assistantText.Append(textContent.Text);

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            break;

                        case FunctionCallContent functionCall:
                            // Use a stable ID across START/ARGS/END — providers may
                            // leave CallId null, so generate a fallback once and reuse it.
                            var toolCallId = functionCall.CallId ?? Guid.NewGuid().ToString("N");

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_START",
                                toolCallId,
                                toolCallName = functionCall.Name,
                                parentMessageId = messageId,
                            }, cancellationToken);

                            if (functionCall.Arguments is { Count: > 0 })
                            {
                                var argsJson = JsonSerializer.Serialize(
                                    functionCall.Arguments, JsonOptions);
                                await WriteSseEventAsync(context.Response, new
                                {
                                    type = "TOOL_CALL_ARGS",
                                    toolCallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId,
                            }, cancellationToken);
                            break;

                        case FunctionResultContent functionResult:
                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_RESULT",
                                messageId = Guid.NewGuid().ToString("N"),
                                toolCallId = functionResult.CallId,
                                content = functionResult.Result?.ToString(),
                                role = "tool",
                            }, cancellationToken);
                            break;

                        case UsageContent usageContent:
                            inputTokens += usageContent.Details.InputTokenCount ?? 0;
                            outputTokens += usageContent.Details.OutputTokenCount ?? 0;
                            break;
                    }
                }
            }

            if (messageStarted)
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);
            }

            stopwatch.Stop();

            // ── Emit RUN_FINISHED with metrics ─────────────────────────
            var metrics = new PlaygroundRunMetrics
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                EstimatedCostUsd = null, // Future: compute from model cost profile
            };

            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_FINISHED",
                runId,
                metrics,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Playground: stream cancelled for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playground: streaming error for run {RunId}", runId);
            try
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "RUN_ERROR",
                    message = ex.Message,
                    code = "INTERNAL_ERROR",
                }, CancellationToken.None);
            }
            catch
            {
                // Connection already broken
            }
        }

        await context.Response.Body.FlushAsync(CancellationToken.None);
    }

    /// <summary>
    /// Builds the <see cref="AIAgent"/> based on the request mode (agent or raw).
    /// </summary>
    private static async Task<AIAgent> BuildAgentAsync(
        PlaygroundRunRequest request,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var chatClient = services.GetRequiredService<IChatClient>();

        if (!string.IsNullOrWhiteSpace(request.AgentName))
        {
            // ── Agent mode ──────────────────────────────────────────────
            return await BuildFromDescriptorAsync(
                request, chatClient, services, logger, cancellationToken);
        }

        // ── Raw mode ────────────────────────────────────────────────────
        logger.LogInformation("Playground: raw mode with custom system prompt");

        return new ChatClientAgent(
            chatClient,
            name: "playground-raw",
            instructions: request.SystemPrompt ?? string.Empty,
            tools: []);
    }

    /// <summary>
    /// Resolves a named <see cref="IDomainAgentDescriptor"/>, applies the
    /// playground overrides (system prompt, tool filter), and builds the agent.
    /// </summary>
    private static async Task<AIAgent> BuildFromDescriptorAsync(
        PlaygroundRunRequest request,
        IChatClient chatClient,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var descriptors = services.GetRequiredService<IEnumerable<IDomainAgentDescriptor>>();
        var descriptor = descriptors.FirstOrDefault(
            d => string.Equals(d.Name, request.AgentName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{request.AgentName}'. " +
                $"Available: {string.Join(", ", descriptors.Select(d => d.Name))}");
        }

        // Use the playground system prompt if provided, otherwise fall back to
        // the database config override, then the code-based descriptor default.
        string? instructionsOverride = null;
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            instructionsOverride = request.SystemPrompt;
        }
        else
        {
            var configService = services.GetRequiredService<IAgentConfigurationService>();
            var config = await configService.GetResolvedAsync(
                request.AgentName!, cancellationToken);

            if (config is not null && !string.IsNullOrWhiteSpace(config.InstructionsText))
                instructionsOverride = config.InstructionsText;
        }

        // Apply tool filter from the playground request
        HashSet<string>? allowedToolNames = request.EnabledToolNames is { Count: > 0 }
            ? new HashSet<string>(request.EnabledToolNames, StringComparer.Ordinal)
            : null;

        var agent = descriptor.Build(chatClient, services, instructionsOverride, allowedToolNames);

        logger.LogInformation(
            "Playground: agent mode '{AgentName}' with {ToolFilter} tool filter, prompt override={PromptOverride}",
            request.AgentName,
            allowedToolNames is not null ? $"{allowedToolNames.Count} tools" : "no",
            instructionsOverride is not null);

        return agent;
    }

    /// <summary>
    /// Builds <see cref="ChatOptions"/> from the request's model, temperature,
    /// and max tokens overrides.
    /// </summary>
    private static async Task<ChatOptions?> BuildChatOptionsAsync(
        PlaygroundRunRequest request,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string? modelName = null;

        if (request.ModelId.HasValue)
        {
            var resolver = services.GetRequiredService<IAiModelResolver>();
            modelName = await resolver.ResolveModelNameByIdAsync(
                request.ModelId.Value, cancellationToken);

            if (modelName is null)
            {
                logger.LogWarning(
                    "Playground: model ID {ModelId} not found — using default",
                    request.ModelId.Value);
            }
        }

        if (modelName is null && request.Temperature is null && request.MaxTokens is null)
            return null;

        return new ChatOptions
        {
            ModelId = modelName,
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxTokens,
        };
    }

    /// <summary>
    /// Converts the playground request into a list of <see cref="ChatMessage"/>
    /// objects, prepending the user brief (if provided) as a system message.
    /// </summary>
    private static List<ChatMessage> BuildChatMessages(PlaygroundRunRequest request)
    {
        var messages = new List<ChatMessage>();

        // Inject user brief as a system message (same format as AguiStreamingEndpoint)
        if (!string.IsNullOrWhiteSpace(request.UserBriefJson))
        {
            var briefContent = $"""
                ## User Brief (current context — treat as ground truth for this session)

                ```json
                {request.UserBriefJson}
                ```
                """;

            messages.Add(new ChatMessage(ChatRole.System, briefContent));
        }

        // Convert conversation messages
        if (request.Messages is { Count: > 0 })
        {
            foreach (var msg in request.Messages)
            {
                var role = msg.Role?.ToLowerInvariant() switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User,
                };

                messages.Add(new ChatMessage(role, msg.Content ?? string.Empty));
            }
        }

        return messages;
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
}
