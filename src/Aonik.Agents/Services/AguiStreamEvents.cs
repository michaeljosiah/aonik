namespace Aonik.Agents.Services;

/// <summary>
/// Single source of truth for the AG-UI streaming event shapes emitted by both
/// <see cref="AguiStreamPipeline"/> and the playground streaming endpoint. Each
/// caller keeps its own loop + transport (AguiResponseWriter vs HttpResponse) and
/// side-effects; only the wire shape of these content-translation events is shared,
/// so a new event type or format tweak lands in one place (M14 / #125).
///
/// Factories return <see cref="object"/>: both call sites serialize with
/// System.Text.Json, which serializes an <c>object</c>-typed value using its RUNTIME
/// type (the anonymous type) — identical JSON to the previous inline objects.
/// </summary>
internal static class AguiStreamEvents
{
    public static object TextMessageStart(string messageId) =>
        new { type = "TEXT_MESSAGE_START", messageId, role = "assistant" };

    public static object TextMessageContent(string messageId, string delta) =>
        new { type = "TEXT_MESSAGE_CONTENT", messageId, delta };

    public static object TextMessageEnd(string messageId) =>
        new { type = "TEXT_MESSAGE_END", messageId };

    public static object ToolCallStart(string toolCallId, string? toolCallName, string parentMessageId) =>
        new { type = "TOOL_CALL_START", toolCallId, toolCallName, parentMessageId };

    public static object ToolCallArgs(string toolCallId, string delta) =>
        new { type = "TOOL_CALL_ARGS", toolCallId, delta };

    public static object ToolCallEnd(string toolCallId) =>
        new { type = "TOOL_CALL_END", toolCallId };

    public static object ToolCallResult(string toolCallId, string? content) =>
        new { type = "TOOL_CALL_RESULT", messageId = Guid.NewGuid().ToString("N"), toolCallId, content, role = "tool" };

    public static object ReasoningMessageContent(string messageId, string delta) =>
        new { type = "REASONING_MESSAGE_CONTENT", messageId, delta };
}
