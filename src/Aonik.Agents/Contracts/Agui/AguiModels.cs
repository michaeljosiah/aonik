using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Agents.Contracts.Agui;

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
    /// named domain agent directly (bypassing the master orchestrator). When
    /// <c>null</c>, the master orchestrator is used. Product apps (e.g. Payabo)
    /// set this to route to a specific domain agent.
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
