using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Voice.Endpoints;

/// <summary>
/// First text frame the mobile client sends after the WebSocket upgrade. Voxa's
/// <c>WireProtocol.TryParseClientMessage()</c> intentionally returns null for
/// <c>hello</c> — consumers parse it manually and snapshot the audio config
/// out-of-band before constructing <c>WebSocketAudioSource</c>. See spec
/// <c>docs/specifications/022.aonik-voice-realtime.md</c> "Endpoint Lifecycle".
/// </summary>
public sealed record HelloEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "hello";

    /// <summary>Required. The agent the user wants to talk to (e.g. <c>"personal-finance-agent"</c>).</summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    /// <summary>Optional. Resume an existing thread; otherwise a new thread is created on first transcription.</summary>
    [JsonPropertyName("chatThreadId")]
    public string? ChatThreadId { get; init; }

    /// <summary>
    /// Frontend tools the client supports — names must be present in the
    /// server-owned <see cref="Tools.IVoiceFrontendToolCatalog"/>. Voice mode
    /// rejects unknown names rather than blindly trusting the client.
    /// </summary>
    [JsonPropertyName("frontendTools")]
    public List<string>? FrontendTools { get; init; }

    /// <summary>Optional client info for telemetry (e.g. app version, device).</summary>
    [JsonPropertyName("client")]
    public Dictionary<string, string>? Client { get; init; }
}

/// <summary>
/// Reads and parses the first <c>hello</c> envelope off a freshly accepted
/// WebSocket. Throws <see cref="HelloParseException"/> on malformed payloads
/// or on the wrong envelope type — caller closes with an <c>error</c> envelope
/// and abandons the connection.
/// </summary>
public static class HelloEnvelopeReader
{
    private const int MaxHelloBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task<HelloEnvelope> ReadAsync(WebSocket socket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(socket);

        using var ms = new MemoryStream();
        var buffer = new byte[4096];

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new HelloParseException("WebSocket closed before hello envelope arrived.");
            }
            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new HelloParseException(
                    $"Expected text frame for hello envelope; got {result.MessageType}.");
            }

            ms.Write(buffer, 0, result.Count);
            if (ms.Length > MaxHelloBytes)
            {
                throw new HelloParseException(
                    $"Hello envelope exceeded {MaxHelloBytes} bytes; aborting.");
            }
        }
        while (!result.EndOfMessage);

        ms.Position = 0;
        HelloEnvelope? hello;
        try
        {
            hello = await JsonSerializer.DeserializeAsync<HelloEnvelope>(ms, JsonOpts, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new HelloParseException("Failed to parse hello envelope as JSON.", ex);
        }

        if (hello is null)
        {
            throw new HelloParseException("Hello envelope deserialised to null.");
        }
        if (!string.Equals(hello.Type, "hello", StringComparison.Ordinal))
        {
            throw new HelloParseException(
                $"Expected envelope type 'hello'; got '{hello.Type}'.");
        }
        if (string.IsNullOrWhiteSpace(hello.AgentId))
        {
            throw new HelloParseException("Hello envelope is missing required 'agentId'.");
        }

        return hello;
    }
}

/// <summary>Thrown when the first WebSocket frame is not a valid hello envelope.</summary>
public sealed class HelloParseException : Exception
{
    public HelloParseException(string message) : base(message) { }
    public HelloParseException(string message, Exception innerException) : base(message, innerException) { }
}
