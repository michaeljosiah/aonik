using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
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
internal sealed class PlaygroundStreamingEndpoint : Endpoint<PlaygroundRunRequest>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IChatClient _chatClient;
    private readonly IAiTaskReader _taskReader;
    private readonly IAgentConfigurationService _agentConfig;
    private readonly IAiModelResolver _modelResolver;
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly IAguiMessageConverter _messageConverter;
    private readonly IToolCallClassifier _toolClassifier;
    private readonly ISpeechRenderer _speechRenderer;
    private readonly ILogger<PlaygroundStreamingEndpoint> _logger;
    private readonly ICurrentUserProvider? _currentUserProvider;

    public PlaygroundStreamingEndpoint(
        IChatClient chatClient,
        IAiTaskReader taskReader,
        IAgentConfigurationService agentConfig,
        IAiModelResolver modelResolver,
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IAguiMessageConverter messageConverter,
        IToolCallClassifier toolClassifier,
        ISpeechRenderer speechRenderer,
        ILogger<PlaygroundStreamingEndpoint> logger,
        ICurrentUserProvider? currentUserProvider = null)
    {
        _chatClient = chatClient;
        _taskReader = taskReader;
        _agentConfig = agentConfig;
        _modelResolver = modelResolver;
        _descriptors = descriptors;
        _messageConverter = messageConverter;
        _toolClassifier = toolClassifier;
        _speechRenderer = speechRenderer;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Post("/ai/playground/run");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Stream a playground agent run";
            s.Description = "Executes an agent or raw prompt in the AI playground with SSE streaming. Supports custom system prompts, model overrides, and tool filters. Playground runs are ephemeral and not persisted.";
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(PlaygroundRunRequest request, CancellationToken cancellationToken)
    {
        var response = HttpContext.Response;

        // Validate: must have an agent name, a system prompt, or an AI task ID
        if (string.IsNullOrWhiteSpace(request.AgentName)
            && string.IsNullOrWhiteSpace(request.SystemPrompt)
            && !request.AiTaskId.HasValue)
        {
            response.StatusCode = 400;
            await response.WriteAsync(
                "Either 'agentName', 'systemPrompt', or 'aiTaskId' is required", cancellationToken);
            return;
        }

        var runId = Guid.NewGuid().ToString("N");
        StampPlaygroundTelemetryContext(request, runId);

        // ── Set SSE headers ─────────────────────────────────────────────
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-store";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        // ── Build the agent ─────────────────────────────────────────────
        AIAgent agent;
        try
        {
            agent = await BuildAgentAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Playground: agent build failed");
            await WriteSseEventAsync(response, new
            {
                type = "RUN_ERROR",
                message = ex.Message,
                code = "AGENT_BUILD_FAILED",
            }, cancellationToken);
            return;
        }

        // ── Build ChatOptions for model/temperature/maxTokens ───────────
        var chatOptions = await BuildChatOptionsAsync(request, cancellationToken);

        var clientTools = _messageConverter.ConvertClientTools(request.ToolDefinitions);
        var clientToolNames = new HashSet<string>(
            clientTools.Select(tool => tool.Name),
            StringComparer.OrdinalIgnoreCase);

        // ── Assemble messages ───────────────────────────────────────────
        // In AI Task mode, if the user template is defined and no user messages
        // were provided, inject the resolved user template as the user message.
        string? aiTaskUserTemplate = null;
        if (request.AiTaskId.HasValue && request is { Messages: null or { Count: 0 } })
        {
            var snapshot = await _taskReader.GetByIdAsync(request.AiTaskId.Value, cancellationToken);
            aiTaskUserTemplate = ApplyVariables(snapshot?.UserTemplate, request.PromptVariables);
        }

        var chatMessages = BuildChatMessages(request, aiTaskUserTemplate);

        // ── Stream ──────────────────────────────────────────────────────
        var stopwatch = Stopwatch.StartNew();
        var assistantText = new StringBuilder();
        long inputTokens = 0;
        long outputTokens = 0;

        try
        {
            await WriteSseEventAsync(response, new
            {
                type = "RUN_STARTED",
                runId,
            }, cancellationToken);

            var messageId = Guid.NewGuid().ToString("N");
            var messageStarted = false;
            var requiresVisualAttention = false;
            var requiresApproval = false;
            var speechBuffer = new SpeechStreamBuffer();

            ChatClientAgentRunOptions? runOptions = null;
            if (chatOptions is not null || clientTools.Count > 0)
            {
                runOptions = new ChatClientAgentRunOptions
                {
                    ChatOptions = chatOptions ?? new ChatOptions(),
                };

                if (clientTools.Count > 0)
                {
                    runOptions.ChatOptions.Tools = clientTools;
                }
            }

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
                                await WriteSseEventAsync(response, new
                                {
                                    type = "TEXT_MESSAGE_START",
                                    messageId,
                                    role = "assistant",
                                }, cancellationToken);
                                messageStarted = true;
                            }

                            assistantText.Append(textContent.Text);
                            speechBuffer.Append(textContent.Text);

                            await WriteSseEventAsync(response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);

                            while (speechBuffer.TryPopSentence(out var rawChunk))
                            {
                                await EmitSpeechChunkAsync(
                                    response, messageId, speechBuffer.NextChunkIndex - 1,
                                    rawChunk, isFinal: false, cancellationToken);
                            }
                            break;

                        case FunctionCallContent functionCall:
                            var toolCallId = _toolClassifier.ResolveCallId(functionCall);
                            var toolName = functionCall.Name ?? string.Empty;
                            requiresVisualAttention |= _toolClassifier.IsDisplay(toolName)
                                || clientToolNames.Contains(toolName);
                            requiresApproval |= _toolClassifier.RequiresApproval(toolName);

                            await WriteSseEventAsync(response, new
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
                                await WriteSseEventAsync(response, new
                                {
                                    type = "TOOL_CALL_ARGS",
                                    toolCallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(response, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId,
                            }, cancellationToken);
                            break;

                        case FunctionResultContent functionResult:
                            await WriteSseEventAsync(response, new
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

                            await WriteSseEventAsync(response, new
                            {
                                type = "REASONING_MESSAGE_CONTENT",
                                messageId,
                                delta = reasoningContent.Text,
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
                await WriteSseEventAsync(response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);
            }

            var tailChunk = speechBuffer.FlushRemaining();
            if (tailChunk is not null)
            {
                await EmitSpeechChunkAsync(
                    response, messageId, speechBuffer.NextChunkIndex - 1,
                    tailChunk, isFinal: true, cancellationToken);
            }

            var guidanceText = _speechRenderer.RenderGuidance(requiresVisualAttention, requiresApproval);
            await WriteSseEventAsync(response, new
            {
                type = "CUSTOM",
                name = "speech.render",
                value = new
                {
                    messageId,
                    speechText = guidanceText,
                    requiresVisualAttention,
                    requiresApproval,
                    isFinal = true,
                }
            }, cancellationToken);

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

            await WriteSseEventAsync(response, new
            {
                type = "RUN_FINISHED",
                runId,
                metrics,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Playground: stream cancelled for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playground: streaming error for run {RunId}", runId);
            try
            {
                await WriteSseEventAsync(response, new
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

        await response.Body.FlushAsync(CancellationToken.None);
    }

    private void StampPlaygroundTelemetryContext(PlaygroundRunRequest request, string runId)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        var sessionId = $"playground-{runId}";
        var mode = ResolvePlaygroundMode(request);

        activity.SetBaggage(AiTelemetry.SessionIdAttribute, sessionId);
        activity.SetTag(AiTelemetry.SessionIdAttribute, sessionId);
        activity.SetTag("aonik.playground.run_id", runId);
        activity.SetTag("aonik.playground.mode", mode);
        activity.SetTag("aonik.playground.agent_name", request.AgentName);
        activity.SetTag("aonik.playground.ai_task_id", request.AiTaskId?.ToString());
        activity.SetTag("aonik.playground.model_id", request.ModelId?.ToString());

        if (_currentUserProvider is not null
            && _currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            var userIdStr = userId.ToString();
            activity.SetBaggage(AiTelemetry.UserIdAttribute, userIdStr);
            activity.SetTag(AiTelemetry.UserIdAttribute, userIdStr);
        }
    }

    private static string ResolvePlaygroundMode(PlaygroundRunRequest request)
    {
        if (request.AiTaskId.HasValue) return "ai-task";
        if (!string.IsNullOrWhiteSpace(request.AgentName)) return "agent";
        return "raw";
    }

    /// <summary>
    /// Builds the <see cref="AIAgent"/> based on the request mode (agent or raw).
    /// </summary>
    private async Task<AIAgent> BuildAgentAsync(
        PlaygroundRunRequest request,
        CancellationToken cancellationToken)
    {
        // ── AI Task mode ────────────────────────────────────────────────
        if (request.AiTaskId.HasValue)
        {
            return await BuildFromAiTaskAsync(request, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.AgentName))
        {
            // ── Agent mode ──────────────────────────────────────────────
            return await BuildFromDescriptorAsync(request, cancellationToken);
        }

        // ── Raw mode ────────────────────────────────────────────────────
        _logger.LogInformation("Playground: raw mode with custom system prompt");

        return new ChatClientAgent(
            _chatClient,
            name: "playground-raw",
            instructions: request.SystemPrompt ?? string.Empty,
            tools: []);
    }

    /// <summary>
    /// Resolves an AI Task by ID, applies variable substitution to its
    /// templates, and builds a simple agent with the resolved prompts.
    /// The user template (with variables applied) is prepended to the
    /// conversation messages as a system message.
    /// </summary>
    private async Task<AIAgent> BuildFromAiTaskAsync(
        PlaygroundRunRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _taskReader.GetByIdAsync(request.AiTaskId!.Value, cancellationToken);

        if (task is null)
        {
            throw new InvalidOperationException(
                $"AI Task with ID '{request.AiTaskId}' was not found or is not published.");
        }

        // Apply variable substitution to templates
        var systemPrompt = ApplyVariables(task.SystemTemplate, request.PromptVariables);

        // Use the playground system prompt override if provided, otherwise use the task's
        var instructions = !string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? request.SystemPrompt
            : systemPrompt ?? string.Empty;

        _logger.LogInformation(
            "Playground: AI Task mode '{TaskName}' (ID: {TaskId}), variables: {VarCount}",
            task.DisplayName,
            request.AiTaskId,
            request.PromptVariables?.Count ?? 0);

        return new ChatClientAgent(
            _chatClient,
            name: $"playground-task-{task.UseCase}",
            instructions: instructions,
            tools: []);
    }

    /// <summary>
    /// Replaces <c>{{variableName}}</c> placeholders in a template with
    /// values from the provided dictionary. Returns the original string
    /// if no variables or template is provided.
    /// </summary>
    private static string? ApplyVariables(string? template, Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(template) || variables is null || variables.Count == 0)
            return template;

        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// Resolves a named <see cref="IDomainAgentDescriptor"/>, applies the
    /// playground overrides (system prompt, tool filter), and builds the agent.
    /// </summary>
    private async Task<AIAgent> BuildFromDescriptorAsync(
        PlaygroundRunRequest request,
        CancellationToken cancellationToken)
    {
        var descriptor = _descriptors.FirstOrDefault(
            d => string.Equals(d.Name, request.AgentName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{request.AgentName}'. " +
                $"Available: {string.Join(", ", _descriptors.Select(d => d.Name))}");
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
            var config = await _agentConfig.GetResolvedAsync(
                request.AgentName!, cancellationToken);

            if (config is not null && !string.IsNullOrWhiteSpace(config.InstructionsText))
                instructionsOverride = config.InstructionsText;
        }

        // Apply tool filter from the playground request.
        // null  = no filter supplied → use all agent tools (default behavior)
        // empty = operator explicitly disabled all tools → pass empty set to suppress tools
        HashSet<string>? allowedToolNames = request.EnabledToolNames is not null
            ? new HashSet<string>(request.EnabledToolNames, StringComparer.Ordinal)
            : null;

        var agent = descriptor.Build(_chatClient, HttpContext.RequestServices, instructionsOverride, allowedToolNames);

        _logger.LogInformation(
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
    private async Task<ChatOptions?> BuildChatOptionsAsync(
        PlaygroundRunRequest request,
        CancellationToken cancellationToken)
    {
        string? modelName = null;

        if (request.ModelId.HasValue)
        {
            modelName = await _modelResolver.ResolveModelNameByIdAsync(
                request.ModelId.Value, cancellationToken);

            if (modelName is null)
            {
                _logger.LogWarning(
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
    private static List<ChatMessage> BuildChatMessages(
        PlaygroundRunRequest request,
        string? aiTaskUserTemplate = null)
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

        // In AI Task mode with no user messages, inject the resolved user template
        if (!string.IsNullOrWhiteSpace(aiTaskUserTemplate) && request.Messages is not { Count: > 0 })
        {
            messages.Add(new ChatMessage(ChatRole.User, aiTaskUserTemplate));
            return messages;
        }

        // Convert conversation messages
        if (request.Messages is { Count: > 0 })
        {
            foreach (var msg in request.Messages)
            {
                var roleName = msg.Role?.ToLowerInvariant();

                switch (roleName)
                {
                    case "assistant":
                    {
                        var contents = new List<AIContent>();

                        if (!string.IsNullOrEmpty(msg.Content))
                        {
                            contents.Add(new TextContent(msg.Content));
                        }

                        if (msg.ToolCalls is { Count: > 0 })
                        {
                            foreach (var toolCall in msg.ToolCalls)
                            {
                                if (toolCall.Function is null || string.IsNullOrWhiteSpace(toolCall.Function.Name))
                                {
                                    continue;
                                }

                                IDictionary<string, object?>? arguments = null;
                                if (!string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
                                {
                                    try
                                    {
                                        arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                            toolCall.Function.Arguments,
                                            JsonOptions);
                                    }
                                    catch
                                    {
                                        arguments = null;
                                    }
                                }

                                contents.Add(new FunctionCallContent(
                                    toolCall.Id,
                                    toolCall.Function.Name,
                                    arguments));
                            }
                        }

                        messages.Add(contents.Count > 0
                            ? new ChatMessage(ChatRole.Assistant, contents)
                            : new ChatMessage(ChatRole.Assistant, msg.Content ?? string.Empty));
                        break;
                    }

                    case "tool":
                    {
                        var toolContent = new FunctionResultContent(
                            msg.ToolCallId ?? string.Empty,
                            msg.Content ?? string.Empty);
                        messages.Add(new ChatMessage(ChatRole.Tool, [toolContent]));
                        break;
                    }

                    case "system":
                        messages.Add(new ChatMessage(ChatRole.System, msg.Content ?? string.Empty));
                        break;

                    default:
                        messages.Add(new ChatMessage(ChatRole.User, msg.Content ?? string.Empty));
                        break;
                }
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

    private async Task EmitSpeechChunkAsync(
        HttpResponse response,
        string messageId,
        int chunkIndex,
        string rawChunk,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        var chunkText = _speechRenderer.RenderChunk(rawChunk);
        if (string.IsNullOrWhiteSpace(chunkText))
            return;

        await WriteSseEventAsync(response, new
        {
            type = "CUSTOM",
            name = "speech.chunk",
            value = new
            {
                messageId,
                chunkIndex,
                speechText = chunkText,
                isFinal,
            },
        }, cancellationToken);
    }
}
