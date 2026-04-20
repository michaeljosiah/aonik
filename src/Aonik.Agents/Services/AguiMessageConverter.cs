using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

public sealed class AguiMessageConverter : IAguiMessageConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly ILogger<AguiMessageConverter> _logger;

    public AguiMessageConverter(ILogger<AguiMessageConverter> logger)
    {
        _logger = logger;
    }

    public List<ChatMessage> ConvertMessages(IEnumerable<AguiMessage>? messages)
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

                    if (!string.IsNullOrEmpty(msg.Content))
                        contents.Add(new TextContent(msg.Content));

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
                                    args = new Dictionary<string, object?> { ["raw"] = tc.Function.Arguments };
                                }
                            }

                            contents.Add(new FunctionCallContent(
                                tc.Id ?? string.Empty,
                                tc.Function.Name ?? string.Empty,
                                args));
                        }
                    }

                    result.Add(new ChatMessage(ChatRole.Assistant, contents));
                    break;
                }

                case "tool":
                {
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
                        "developer" => ChatRole.System,
                        _ => ChatRole.User,
                    };

                    result.Add(new ChatMessage(role, msg.Content ?? string.Empty));
                    break;
                }
            }
        }

        return result;
    }

    public List<AITool> ConvertClientTools(List<JsonElement>? toolElements)
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
                    _logger.LogWarning("AG-UI client tool missing 'name', skipping");
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

                _logger.LogDebug("AG-UI: registered client tool declaration '{ToolName}'", name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AG-UI: failed to parse client tool element, skipping");
            }
        }

        return tools;
    }
}
