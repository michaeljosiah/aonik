using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Wire-format tripwire for the shared AG-UI event factory (M14 / #125). The
/// whole point of centralizing these shapes is that a format change happens in
/// one place — so pin the serialized JSON here. If a factory edit renames,
/// reorders, adds, or drops a property, one of these breaks. Options mirror the
/// endpoints' serializer (camelCase + omit-null), which for these already-camel
/// property names differs only by the null omission on the nullable fields.
/// </summary>
public class AguiStreamEventsTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string Json(object evt) => JsonSerializer.Serialize(evt, WireOptions);

    [Fact]
    public void TextMessageStart_HasExactShape() =>
        Json(AguiStreamEvents.TextMessageStart("m1"))
            .Should().Be("""{"type":"TEXT_MESSAGE_START","messageId":"m1","role":"assistant"}""");

    [Fact]
    public void TextMessageContent_HasExactShape() =>
        Json(AguiStreamEvents.TextMessageContent("m1", "hi"))
            .Should().Be("""{"type":"TEXT_MESSAGE_CONTENT","messageId":"m1","delta":"hi"}""");

    [Fact]
    public void TextMessageEnd_HasExactShape() =>
        Json(AguiStreamEvents.TextMessageEnd("m1"))
            .Should().Be("""{"type":"TEXT_MESSAGE_END","messageId":"m1"}""");

    [Fact]
    public void ToolCallStart_HasExactShape() =>
        Json(AguiStreamEvents.ToolCallStart("t1", "do_thing", "m1"))
            .Should().Be("""{"type":"TOOL_CALL_START","toolCallId":"t1","toolCallName":"do_thing","parentMessageId":"m1"}""");

    [Fact]
    public void ToolCallStart_OmitsNullToolName() =>
        Json(AguiStreamEvents.ToolCallStart("t1", null, "m1"))
            .Should().Be("""{"type":"TOOL_CALL_START","toolCallId":"t1","parentMessageId":"m1"}""");

    [Fact]
    public void ToolCallArgs_HasExactShape() =>
        Json(AguiStreamEvents.ToolCallArgs("t1", "argsdelta"))
            .Should().Be("""{"type":"TOOL_CALL_ARGS","toolCallId":"t1","delta":"argsdelta"}""");

    [Fact]
    public void ToolCallEnd_HasExactShape() =>
        Json(AguiStreamEvents.ToolCallEnd("t1"))
            .Should().Be("""{"type":"TOOL_CALL_END","toolCallId":"t1"}""");

    [Fact]
    public void ToolCallResult_HasExactShape_WithFreshMessageId()
    {
        // messageId is a fresh 32-char hex GUID per emit, so pin the stable fields
        // + the id shape rather than an exact string.
        var json = Json(AguiStreamEvents.ToolCallResult("t1", "done"));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("type").GetString().Should().Be("TOOL_CALL_RESULT");
        root.GetProperty("toolCallId").GetString().Should().Be("t1");
        root.GetProperty("content").GetString().Should().Be("done");
        root.GetProperty("role").GetString().Should().Be("tool");
        root.GetProperty("messageId").GetString().Should().MatchRegex("^[0-9a-f]{32}$");
        // Property order is TOOL_CALL_RESULT: type, messageId, toolCallId, content, role.
        root.EnumerateObject().Select(p => p.Name)
            .Should().Equal("type", "messageId", "toolCallId", "content", "role");
    }

    [Fact]
    public void ToolCallResult_OmitsNullContent()
    {
        var json = Json(AguiStreamEvents.ToolCallResult("t1", null));
        json.Should().NotContain("content");
        JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name)
            .Should().Equal("type", "messageId", "toolCallId", "role");
    }

    [Fact]
    public void ReasoningMessageContent_HasExactShape() =>
        Json(AguiStreamEvents.ReasoningMessageContent("m1", "thinking"))
            .Should().Be("""{"type":"REASONING_MESSAGE_CONTENT","messageId":"m1","delta":"thinking"}""");
}
