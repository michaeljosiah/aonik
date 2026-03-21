using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AG-UI protocol streaming endpoint. Implements the AG-UI SSE protocol
/// using a custom minimal API endpoint that resolves the scoped
/// <see cref="IMasterOrchestratorService"/> per-request (required because
/// the orchestrator depends on scoped <c>IChatClient</c>).
///
/// Protocol: POST with JSON body → SSE response with AG-UI events.
/// Reference: https://docs.ag-ui.com/concepts/events
/// </summary>
public static class AguiStreamingEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Maps the AG-UI streaming endpoint at the specified route pattern.
    /// </summary>
    public static IEndpointConventionBuilder MapAguiStreaming(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/agui")
    {
        return endpoints.MapPost(pattern, HandleAguiRequest)
            .WithName("AgUiStreaming")
            .WithTags("AI");
    }

    private static async Task HandleAguiRequest(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices.GetRequiredService<ILogger<IMasterOrchestratorService>>();

        // Parse the AG-UI request body
        AguiRunInput? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<AguiRunInput>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize AG-UI request body");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid request body", cancellationToken);
            return;
        }

        if (input is null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Request body is required", cancellationToken);
            return;
        }

        var threadId = input.ThreadId ?? Guid.NewGuid().ToString("N");
        var runId = input.RunId ?? Guid.NewGuid().ToString("N");

        // Set SSE response headers
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        // Resolve scoped orchestrator
        var orchestrator = context.RequestServices.GetRequiredService<IMasterOrchestratorService>();

        try
        {
            var agent = await orchestrator.GetAgentAsync(cancellationToken);

            // Convert AG-UI messages to M.E.AI ChatMessage list
            var chatMessages = ConvertMessages(input.Messages);

            // Convert frontend tool definitions to declaration-only AITool instances.
            // These are passed to the LLM so it knows the tools exist, but they cannot
            // be invoked server-side — the agent framework emits them as FunctionCallContent
            // in the stream, and the frontend handles execution via the re-run loop.
            // Pattern follows official MAF AG-UI: AGUIEndpointRouteBuilderExtensions.cs
            var clientTools = ConvertClientTools(input.Tools, logger);
            var clientToolNames = new HashSet<string>(
                clientTools.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            // Build run options with client tools so the LLM sees them alongside
            // the server-side domain agent tools already registered on the agent.
            ChatClientAgentRunOptions? runOptions = null;
            if (clientTools.Count > 0)
            {
                runOptions = new ChatClientAgentRunOptions
                {
                    ChatOptions = new ChatOptions
                    {
                        Tools = clientTools,
                    },
                };
                logger.LogDebug(
                    "AG-UI run {RunId}: passing {ToolCount} client tool(s) to agent: {ToolNames}",
                    runId, clientTools.Count, string.Join(", ", clientToolNames));
            }

            // Emit RUN_STARTED
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_STARTED",
                threadId,
                runId,
            }, cancellationToken);

            // Stream the agent response
            var messageId = Guid.NewGuid().ToString("N");
            var messageStarted = false;

            await foreach (var update in agent.RunStreamingAsync(
                chatMessages, session: null, options: runOptions, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Convert AgentResponseUpdate to ChatResponseUpdate
                var chatUpdate = update.AsChatResponseUpdate();
                if (chatUpdate is null) continue;

                // Process each content item in the update
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

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            break;

                        case FunctionCallContent functionCall:
                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_START",
                                toolCallId = functionCall.CallId ?? Guid.NewGuid().ToString("N"),
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
                                    toolCallId = functionCall.CallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId = functionCall.CallId,
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
                    }
                }

                // Note: chatUpdate.Text is the aggregated text from Contents,
                // so we don't emit it separately to avoid duplicate text deltas.
            }

            // Close message if one was started
            if (messageStarted)
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);
            }

            // Emit RUN_FINISHED
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_FINISHED",
                threadId,
                runId,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected, nothing to do
            logger.LogDebug("AG-UI stream cancelled for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AG-UI streaming error for thread {ThreadId}, run {RunId}", threadId, runId);

            // Try to send RUN_ERROR before closing
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
                // If we can't write the error event, the connection is already broken
            }
        }

        await context.Response.Body.FlushAsync(CancellationToken.None);
    }

    /// <summary>
    /// Converts AG-UI frontend tool definitions (raw JSON) into declaration-only
    /// <see cref="AITool"/> instances using <see cref="AIFunctionFactory.CreateDeclaration"/>.
    /// These tools describe functions the LLM can call but that execute client-side.
    /// Pattern follows official MAF AG-UI source: AIToolExtensions.AsAITools().
    /// </summary>
    private static List<AITool> ConvertClientTools(
        List<JsonElement>? toolElements,
        ILogger logger)
    {
        if (toolElements is null || toolElements.Count == 0)
            return [];

        var tools = new List<AITool>(toolElements.Count);

        foreach (var element in toolElements)
        {
            try
            {
                var name = element.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(name))
                {
                    logger.LogWarning("AG-UI client tool missing 'name', skipping");
                    continue;
                }

                var description = element.TryGetProperty("description", out var descProp)
                    ? descProp.GetString()
                    : null;

                var parameters = element.TryGetProperty("parameters", out var paramsProp)
                    ? paramsProp
                    : default;

                tools.Add(AIFunctionFactory.CreateDeclaration(
                    name: name,
                    description: description,
                    jsonSchema: parameters));

                logger.LogDebug("AG-UI: registered client tool declaration '{ToolName}'", name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AG-UI: failed to parse client tool element, skipping");
            }
        }

        return tools;
    }

    /// <summary>
    /// Converts AG-UI input messages to M.E.AI <see cref="ChatMessage"/> objects.
    /// Handles all AG-UI message types including assistant messages with tool calls
    /// and tool result messages with toolCallId references.
    /// </summary>
    private static List<ChatMessage> ConvertMessages(IEnumerable<AguiMessage>? messages)
    {
        if (messages is null)
            return [];

        var result = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            var roleName = msg.Role?.ToLowerInvariant();

            switch (roleName)
            {
                case "assistant":
                {
                    var contents = new List<AIContent>();

                    // Add text content if present
                    if (!string.IsNullOrEmpty(msg.Content))
                        contents.Add(new TextContent(msg.Content));

                    // Add function call content items for each tool call
                    if (msg.ToolCalls is { Count: > 0 })
                    {
                        foreach (var tc in msg.ToolCalls)
                        {
                            if (tc.Function is null) continue;

                            IDictionary<string, object?>? args = null;
                            if (!string.IsNullOrEmpty(tc.Function.Arguments))
                            {
                                try
                                {
                                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        tc.Function.Arguments, JsonOptions);
                                }
                                catch
                                {
                                    // If arguments fail to parse, pass as a single "raw" key
                                    args = new Dictionary<string, object?> { ["raw"] = tc.Function.Arguments };
                                }
                            }

                            contents.Add(new FunctionCallContent(tc.Id ?? string.Empty, tc.Function.Name ?? string.Empty, args));
                        }
                    }

                    result.Add(new ChatMessage(ChatRole.Assistant, contents));
                    break;
                }

                case "tool":
                {
                    // Tool result message — wraps content in FunctionResultContent
                    var toolContent = new FunctionResultContent(
                        msg.ToolCallId ?? string.Empty,
                        msg.Content ?? string.Empty);

                    result.Add(new ChatMessage(ChatRole.Tool, [toolContent]));
                    break;
                }

                default:
                {
                    var role = roleName switch
                    {
                        "user" => ChatRole.User,
                        "system" => ChatRole.System,
                        "developer" => ChatRole.System, // Map developer to system
                        _ => ChatRole.User,
                    };

                    result.Add(new ChatMessage(role, msg.Content ?? string.Empty));
                    break;
                }
            }
        }

        return result;
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

/// <summary>
/// AG-UI run input DTO. Mirrors the AG-UI protocol's request body.
/// </summary>
public sealed class AguiRunInput
{
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("messages")]
    public List<AguiMessage>? Messages { get; set; }

    [JsonPropertyName("state")]
    public JsonElement? State { get; set; }

    [JsonPropertyName("tools")]
    public List<JsonElement>? Tools { get; set; }

    [JsonPropertyName("context")]
    public List<JsonElement>? Context { get; set; }

    [JsonPropertyName("forwardedProps")]
    public JsonElement? ForwardedProperties { get; set; }
}

/// <summary>
/// AG-UI message DTO within the run input.
/// Supports all AG-UI message types: user, assistant (with tool calls),
/// system, developer, tool (with toolCallId), reasoning, and activity.
/// </summary>
public sealed class AguiMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Tool calls made by an assistant message.
    /// </summary>
    [JsonPropertyName("toolCalls")]
    public List<AguiToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// For tool messages: the ID of the tool call this message responds to.
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// For tool messages: error message if the tool call failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Encrypted content for privacy-preserving state continuity.
    /// </summary>
    [JsonPropertyName("encryptedContent")]
    public string? EncryptedContent { get; set; }

    /// <summary>
    /// Encrypted value for reasoning/tool messages.
    /// </summary>
    [JsonPropertyName("encryptedValue")]
    public string? EncryptedValue { get; set; }

    /// <summary>
    /// For activity messages: the activity type discriminator.
    /// </summary>
    [JsonPropertyName("activityType")]
    public string? ActivityType { get; set; }
}

/// <summary>
/// AG-UI tool call DTO within an assistant message.
/// </summary>
public sealed class AguiToolCall
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public AguiFunctionCall? Function { get; set; }

    [JsonPropertyName("encryptedValue")]
    public string? EncryptedValue { get; set; }
}

/// <summary>
/// AG-UI function call DTO within a tool call.
/// </summary>
public sealed class AguiFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
