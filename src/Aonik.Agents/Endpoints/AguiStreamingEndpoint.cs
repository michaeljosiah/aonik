using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
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
    private static readonly Regex MultiWhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex LeadingListMarkerRegex = new(@"^\s*(?:[-*•]+|\d+[.)])\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingHeadingRegex = new(@"^\s*#+\s*", RegexOptions.Compiled);
    private static readonly Regex SpeechPreambleRegex = new(
        @"^(?:(?:here(?:'s| is)(?:\s+a)?\s+quick\s+summary|here(?:'s| is)\s+the\s+summary|quick\s+summary|summary|in\s+summary|overall|to\s+summari[sz]e)\s*:?\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const string SupportedSpeechCurrencyCodes = "USD|EUR|GBP|NGN|GHS|ZAR|ZWL|ZIG|KES|INR|CNY";
    private static readonly Regex CurrencyBeforeAmountRegex = new(
        $@"\b(?<code>{SupportedSpeechCurrencyCodes})\s*(?<amount>[+-]?\d[\d,]*(?:\.\d+)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountBeforeCurrencyRegex = new(
        $@"\b(?<amount>[+-]?\d[\d,]*(?:\.\d+)?)\s*(?<code>{SupportedSpeechCurrencyCodes})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, SpokenCurrencyDescriptor> SpokenCurrencies =
        new Dictionary<string, SpokenCurrencyDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new("dollar", "dollars"),
            ["EUR"] = new("euro", "euros"),
            ["GBP"] = new("pound", "pounds"),
            ["NGN"] = new("naira", "naira"),
            ["GHS"] = new("cedi", "cedis"),
            ["ZAR"] = new("rand", "rand"),
            ["ZWL"] = new("Zimbabwe dollar", "Zimbabwe dollars"),
            ["ZIG"] = new("Zimbabwe Gold", "Zimbabwe Gold"),
            ["KES"] = new("Kenyan shilling", "Kenyan shillings"),
            ["INR"] = new("rupee", "rupees"),
            ["CNY"] = new("yuan", "yuan")
        };

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

        // Propagate session (threadId) and user identifiers as OTel baggage + span
        // attributes so the BaggageSpanProcessor copies them to all child spans,
        // enabling Langfuse session grouping and user attribution.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(AiTelemetry.SessionIdAttribute, threadId);
            activity.SetTag(AiTelemetry.SessionIdAttribute, threadId);

            var userProvider = context.RequestServices.GetService<ICurrentUserProvider>();
            if (userProvider is not null && userProvider.TryGetCurrentUserId(out var userId))
            {
                var userIdStr = userId.ToString();
                activity.SetBaggage(AiTelemetry.UserIdAttribute, userIdStr);
                activity.SetTag(AiTelemetry.UserIdAttribute, userIdStr);
            }
        }

        // ── Thread persistence: create or load thread ────────────────────
        var chatThreadService = context.RequestServices.GetService<IChatThreadService>();
        var titleGenerator = context.RequestServices.GetService<IChatThreadTitleGenerator>();

        Guid? persistedThreadId = null;
        var isNewThread = false;
        string? firstUserMessage = null;

        if (chatThreadService is not null)
        {
            try
            {
                // Extract the last user message from the AG-UI messages
                firstUserMessage = input.Messages?
                    .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                    ?.Content;

                if (!string.IsNullOrEmpty(firstUserMessage))
                {
                    // Check if the threadId maps to an existing persisted thread
                    if (Guid.TryParse(input.ThreadId, out var existingId))
                    {
                        persistedThreadId = existingId;

                        await chatThreadService.AppendMessageAsync(
                            existingId, "user", firstUserMessage,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Create a new thread
                        persistedThreadId = await chatThreadService.CreateThreadAsync(
                            firstUserMessage,
                            agentName: input.AgentId,
                            cancellationToken: cancellationToken);
                        isNewThread = true;

                        // Update threadId to use the persisted thread ID for SSE events
                        threadId = persistedThreadId.Value.ToString("N");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AG-UI thread persistence failed — continuing without thread tracking");
            }
        }

        // Set SSE response headers
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        // Resolve the agent to stream against.
        // Named agentId → build the domain agent descriptor directly; if the descriptor
        //   declares RequiresUserBrief, the User Brief is projected and prepended as a
        //   system message so the agent has full user context before responding.
        // No agentId → master orchestrator (Admin UI default).
        AIAgent agent;
        List<ChatMessage>? userBriefPreamble = null;

        if (!string.IsNullOrEmpty(input.AgentId))
        {
            var (builtAgent, descriptor) = await ResolveDomainAgentAsync(
                input.AgentId, context.RequestServices, logger, cancellationToken);
            agent = builtAgent;

            // If the descriptor declares it needs the User Brief, project it now
            // and prepend as a system message so the persona has full user context.
            if (descriptor.RequiresUserBrief)
            {
                var userProvider = context.RequestServices.GetService<ICurrentUserProvider>();
                var tenantProvider = context.RequestServices.GetService<ITenantProvider>();
                if (userProvider is not null
                    && tenantProvider is not null
                    && userProvider.TryGetCurrentUserId(out var briefUserId)
                    && tenantProvider.TryGetCurrentTenantId(out var briefTenantId))
                {
                    var briefMessage = await BuildUserBriefMessageAsync(
                        context.RequestServices, briefTenantId, briefUserId,
                        logger, cancellationToken);
                    if (briefMessage is not null)
                        userBriefPreamble = [briefMessage];
                }
            }
        }
        else
        {
            var orchestrator = context.RequestServices.GetRequiredService<IMasterOrchestratorService>();
            agent = await orchestrator.GetAgentAsync(cancellationToken);
        }

        // Accumulate streamed assistant text for post-stream persistence
        var assistantTextBuilder = new System.Text.StringBuilder();

        try
        {
            // Convert AG-UI messages to M.E.AI ChatMessage list.
            // For agents that declare RequiresUserBrief, the brief is prepended as a
            // system message so the LLM receives current user context before the history.
            var chatMessages = ConvertMessages(input.Messages);
            if (userBriefPreamble is not null)
                chatMessages = [.. userBriefPreamble, .. chatMessages];

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
            var requiresVisualAttention = false;
            var requiresApproval = false;

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

                            assistantTextBuilder.Append(textContent.Text);

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            break;

                        case FunctionCallContent functionCall:
                            var toolName = functionCall.Name ?? string.Empty;
                            requiresVisualAttention |= IsDisplayToolCall(toolName);
                            requiresApproval |= IsApprovalToolCall(toolName);

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

                        case TextReasoningContent reasoningContent
                            when !string.IsNullOrEmpty(reasoningContent.Text):

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "REASONING_MESSAGE_CONTENT",
                                messageId,
                                delta = reasoningContent.Text,
                            }, cancellationToken);
                            break;
                    }
                }

                // Note: chatUpdate.Text is the aggregated text from Contents,
                // so we don't emit it separately to avoid duplicate text deltas.
            }

            var assistantText = assistantTextBuilder.ToString();

            // Close message if one was started
            if (messageStarted)
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);

            }

            var speechText = BuildSpeechRender(assistantText, requiresVisualAttention, requiresApproval);
            if (!string.IsNullOrWhiteSpace(speechText))
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "CUSTOM",
                    name = "speech.render",
                    value = new
                    {
                        messageId,
                        speechText,
                        requiresVisualAttention,
                        requiresApproval
                    }
                }, cancellationToken);
            }

            // Emit RUN_FINISHED
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_FINISHED",
                threadId,
                runId,
            }, cancellationToken);

            // ── Post-stream thread persistence ──────────────────────────
            // Persist the assistant response and generate a title for new
            // threads. Failures here must never block the completed stream.
            if (chatThreadService is not null && persistedThreadId.HasValue)
            {
                try
                {
                    if (!string.IsNullOrEmpty(assistantText))
                    {
                        await chatThreadService.AppendMessageAsync(
                            persistedThreadId.Value,
                            "assistant",
                            assistantText,
                            agentName: input.AgentId,
                            cancellationToken: CancellationToken.None);
                    }

                    // Generate a real title for brand-new threads
                    if (isNewThread && titleGenerator is not null && !string.IsNullOrEmpty(firstUserMessage))
                    {
                        try
                        {
                            var title = await titleGenerator.GenerateTitleAsync(
                                firstUserMessage, CancellationToken.None);
                            await chatThreadService.UpdateTitleAsync(
                                persistedThreadId.Value, title, CancellationToken.None);
                        }
                        catch (Exception titleEx)
                        {
                            logger.LogWarning(
                                titleEx,
                                "AG-UI title generation failed for thread {ThreadId} — placeholder title retained",
                                persistedThreadId.Value);
                        }
                    }
                }
                catch (Exception persistEx)
                {
                    logger.LogWarning(
                        persistEx,
                        "AG-UI post-stream persistence failed for thread {ThreadId}",
                        persistedThreadId.Value);
                }
            }
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
    /// Resolves a named <see cref="IDomainAgentDescriptor"/> by its agent name,
    /// applies any database-level configuration overrides (instructions, tool set,
    /// active flag), and builds the domain agent. Returns both the built agent and
    /// the descriptor so the caller can inspect flags like
    /// <see cref="IDomainAgentDescriptor.RequiresUserBrief"/>.
    /// </summary>
    private static async Task<(AIAgent Agent, IDomainAgentDescriptor Descriptor)> ResolveDomainAgentAsync(
        string agentId,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var descriptors = services.GetRequiredService<IEnumerable<IDomainAgentDescriptor>>();
        var descriptor = descriptors.FirstOrDefault(
            d => string.Equals(d.Name, agentId, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{agentId}'. " +
                $"Available: {string.Join(", ", descriptors.Select(d => d.Name))}");
        }

        var configService = services.GetRequiredService<IAgentConfigurationService>();
        var config = await configService.GetResolvedAsync(agentId, cancellationToken);

        if (config is { IsActive: false })
            throw new InvalidOperationException($"Agent '{agentId}' is inactive per configuration.");

        var chatClient = services.GetRequiredService<IChatClient>();
        AIAgent agent;

        if (config is not null)
        {
            var instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                ? config.InstructionsText
                : null;

            HashSet<string>? allowedToolNames = null;
            if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson) && config.ToolsetIdsJson != "[]")
            {
                try
                {
                    var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                    if (toolNames is { Count: > 0 })
                        allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid ToolsetIdsJson for agent '{AgentName}' — using all tools", agentId);
                }
            }

            agent = descriptor.Build(chatClient, services, instructionsOverride, allowedToolNames);
            logger.LogInformation("AG-UI: resolved domain agent '{AgentName}' with config override", agentId);
        }
        else
        {
            agent = descriptor.Build(chatClient, services);
            logger.LogInformation("AG-UI: resolved domain agent '{AgentName}' with code defaults", agentId);
        }

        return (agent, descriptor);
    }

    /// <summary>
    /// Projects the User Brief for the given user and returns it as a
    /// <see cref="ChatMessage"/> with <see cref="ChatRole.System"/> role,
    /// ready to be prepended to the conversation history.
    /// Returns <c>null</c> if projection fails (agent proceeds without brief).
    /// </summary>
    private static async Task<ChatMessage?> BuildUserBriefMessageAsync(
        IServiceProvider services,
        Guid tenantId,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var projector = services.GetRequiredService<IUserBriefProjector>();
            var brief = await projector.ProjectAsync(tenantId, userId, cancellationToken: cancellationToken);

            var briefJson = JsonSerializer.Serialize(brief, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });

            var content = $"""
                ## User Brief (current context — treat as ground truth for this session)

                ```json
                {briefJson}
                ```
                """;

            return new ChatMessage(ChatRole.System, content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to build User Brief for user {UserId} in tenant {TenantId} — proceeding without brief",
                userId, tenantId);
            return null;
        }
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

    internal static string BuildSpeechRender(
        string assistantText,
        bool requiresVisualAttention = false,
        bool requiresApproval = false)
    {
        var speechText = string.Empty;
        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            var normalized = assistantText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            normalized = MarkdownLinkRegex.Replace(normalized, "${text}");

            var lines = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeSpeechLine)
                .Where(line => !string.IsNullOrWhiteSpace(line));

            speechText = string.Join(' ', lines);
        }

        speechText = SpeechPreambleRegex.Replace(speechText, string.Empty);
        speechText = speechText.Replace(" & ", " and ", StringComparison.Ordinal);
        speechText = ExpandCurrencyAmounts(speechText);
        speechText = Regex.Replace(speechText, @"\s+([,.;!?])", "$1");
        speechText = MultiWhitespaceRegex.Replace(speechText, " ").Trim();
        speechText = AppendChatReviewGuidance(speechText, requiresVisualAttention, requiresApproval);

        return speechText;
    }

    private static string AppendChatReviewGuidance(
        string speechText,
        bool requiresVisualAttention,
        bool requiresApproval)
    {
        var guidance = BuildChatReviewGuidance(requiresVisualAttention, requiresApproval);
        if (string.IsNullOrWhiteSpace(guidance))
        {
            return speechText;
        }

        if (string.IsNullOrWhiteSpace(speechText))
        {
            return guidance;
        }

        var normalized = speechText.TrimEnd();
        if (normalized.EndsWith(guidance, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var separator = normalized.EndsWith('.') || normalized.EndsWith('!') || normalized.EndsWith('?')
            ? " "
            : ". ";

        return $"{normalized}{separator}{guidance}";
    }

    private static string BuildChatReviewGuidance(bool requiresVisualAttention, bool requiresApproval)
    {
        if (requiresVisualAttention && requiresApproval)
        {
            return "I've opened the chat so you can review the details and approve this action.";
        }

        if (requiresApproval)
        {
            return "I've opened the chat so you can review and approve this action.";
        }

        if (requiresVisualAttention)
        {
            return "I've opened the chat so you can review the details.";
        }

        return string.Empty;
    }

    private static string NormalizeSpeechLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var wasListItem = LeadingListMarkerRegex.IsMatch(trimmed);
        trimmed = LeadingHeadingRegex.Replace(trimmed, string.Empty);
        trimmed = LeadingListMarkerRegex.Replace(trimmed, string.Empty);
        trimmed = trimmed
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace('`', ' ');
        trimmed = MultiWhitespaceRegex.Replace(trimmed, " ").Trim();

        if (wasListItem && trimmed.Length > 0 && !trimmed.EndsWith('.') && !trimmed.EndsWith('!') && !trimmed.EndsWith('?'))
        {
            trimmed = $"{trimmed}.";
        }

        return trimmed;
    }

    private static string ExpandCurrencyAmounts(string value)
    {
        var expanded = CurrencyBeforeAmountRegex.Replace(value, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        expanded = AmountBeforeCurrencyRegex.Replace(expanded, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        return expanded;
    }

    private static string BuildSpokenAmount(string amount, string currencyCode)
    {
        if (!SpokenCurrencies.TryGetValue(currencyCode, out var descriptor))
        {
            return $"{amount} {currencyCode}";
        }

        var normalizedAmount = amount.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            return $"{amount} {descriptor.Plural}";
        }

        var currencyName = decimal.Abs(parsedAmount) == 1m
            ? descriptor.Singular
            : descriptor.Plural;

        return $"{amount} {currencyName}";
    }

    private static bool IsDisplayToolCall(string toolName)
    {
        return toolName.StartsWith("display_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApprovalToolCall(string toolName)
    {
        return string.Equals(toolName, "confirmAction", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SpokenCurrencyDescriptor(string Singular, string Plural);
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

    /// <summary>
    /// Optional agent identifier. When set, the endpoint resolves and runs the
    /// named <see cref="IDomainAgentDescriptor"/> directly (bypassing the master
    /// orchestrator). When <c>null</c>, the master orchestrator is used.
    /// Product apps (e.g. Payabo) set this to route to a specific domain agent.
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

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
