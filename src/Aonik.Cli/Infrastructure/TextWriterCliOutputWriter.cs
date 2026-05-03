using System.Text;
using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Infrastructure;

public sealed class TextWriterCliOutputWriter : ICliOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly TextWriter _writer;

    public TextWriterCliOutputWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public Task WriteInfoAsync(string message, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return _writer.WriteLineAsync(message);
    }

    public Task WriteObjectAsync<T>(T value, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        return WriteValueAsync(value, outputMode, cancellationToken);
    }

    public Task WriteCollectionAsync<T>(IReadOnlyList<T> values, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        return WriteValueAsync(values, outputMode, cancellationToken);
    }

    public Task WriteSessionAsync(CliSession session, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(session, outputMode, cancellationToken);
        }

        return WriteLinesAsync(
            [
                $"AONIK session: {session.BaseUrl}",
                $"- provider: {session.ActiveProvider ?? "unknown"}",
                $"- user: {session.Email ?? "unknown"}",
                $"- tenant: {(session.TenantId?.ToString("D") ?? "not set")}",
                $"- expires: {(session.ExpiresAt?.ToString("O") ?? "not set")}",
                $"- last session: {session.LastSessionId ?? "not set"}",
                $"- last thread: {session.LastThreadId ?? "not set"}"
            ]);
    }

    public Task WriteUserInfoAsync(UserInfoResponseDto userInfo, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(userInfo, outputMode, cancellationToken);
        }

        return WriteLinesAsync(
            [
                $"Authenticated user: {userInfo.Email}",
                $"- userId: {userInfo.UserId:D}",
                $"- tenantId: {userInfo.TenantId:D}",
                $"- partyId: {userInfo.PartyId:D}",
                $"- roles: {(userInfo.Roles.Count == 0 ? "none" : string.Join(", ", userInfo.Roles))}"
            ]);
    }

    public Task WriteAgentsAsync(IReadOnlyList<AgentInfo> agents, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(agents, outputMode, cancellationToken);
        }

        if (agents.Count == 0)
        {
            return WriteLinesAsync(["No agents registered."]);
        }

        var lines = new List<string> { "Available agents:" };
        lines.AddRange(agents.Select(agent => $"- {agent.Name}: {agent.Description}"));
        return WriteLinesAsync(lines);
    }

    public Task WriteChatResponseAsync(AgentChatResponse response, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(response, outputMode, cancellationToken);
        }

        return WriteLinesAsync(
            [
                response.Message,
                string.Empty,
                $"- agent: {response.AgentName ?? "orchestrator"}",
                $"- session: {response.SessionId}",
                $"- thread: {response.ThreadId ?? "not set"}",
                $"- title: {response.ThreadTitle ?? "not set"}"
            ]);
    }

    public Task WriteThreadsAsync(IReadOnlyList<ChatThreadSummary> threads, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(threads, outputMode, cancellationToken);
        }

        if (threads.Count == 0)
        {
            return WriteLinesAsync(["No chat threads found."]);
        }

        var lines = new List<string> { "Chat threads:" };
        lines.AddRange(threads.Select(thread =>
            $"- {thread.Id:D} | {thread.Title} | {thread.Status} | messages={thread.MessageCount}"));
        return WriteLinesAsync(lines);
    }

    public Task WriteThreadAsync(ChatThreadDetail thread, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(thread, outputMode, cancellationToken);
        }

        var lines = new List<string>
        {
            $"Thread {thread.Id:D}",
            $"- title: {thread.Title}",
            $"- status: {thread.Status}",
            $"- agent: {thread.AgentName ?? "not set"}",
            $"- messages: {thread.MessageCount}",
            string.Empty
        };

        foreach (var message in thread.Messages.OrderBy(m => m.SortOrder))
        {
            lines.Add($"[{message.Role}] {message.Content}");
        }

        return WriteLinesAsync(lines);
    }

    public Task WriteStreamEventAsync(AgentStreamEvent streamEvent, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (outputMode != OutputMode.Text)
        {
            return WriteValueAsync(streamEvent, outputMode, cancellationToken);
        }

        return _writer.WriteLineAsync(RenderStreamEvent(streamEvent));
    }

    private Task WriteValueAsync<T>(T value, OutputMode outputMode, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var json = JsonSerializer.Serialize(value, JsonOptions);
        return _writer.WriteLineAsync(json);
    }

    private async Task WriteLinesAsync(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            await _writer.WriteLineAsync(line);
        }
    }

    internal static string RenderStreamEvent(AgentStreamEvent streamEvent)
    {
        if (string.Equals(streamEvent.Type, "TEXT_MESSAGE_CONTENT", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(streamEvent.Json);
            if (document.RootElement.TryGetProperty("delta", out var delta))
            {
                return delta.GetString() ?? string.Empty;
            }
        }

        if (string.Equals(streamEvent.Type, "CUSTOM", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamEvent.Name, "speech.render", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(streamEvent.Json);
            if (document.RootElement.TryGetProperty("value", out var value)
                && value.TryGetProperty("speechText", out var speechText))
            {
                return $"[speech.render] {speechText.GetString()}";
            }
        }

        // speech.audio carries base64-encoded audio bytes — render a one-line
        // summary instead of dumping the payload to the terminal. Same hook
        // can short-circuit any future binary CUSTOM event.
        if (string.Equals(streamEvent.Type, "CUSTOM", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamEvent.Name, "speech.audio", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(streamEvent.Json);
            if (document.RootElement.TryGetProperty("value", out var value))
            {
                var chunkIndex = value.TryGetProperty("chunkIndex", out var chunkProp) ? chunkProp.GetInt32() : -1;
                var seq = value.TryGetProperty("seq", out var seqProp) ? seqProp.GetInt32() : -1;
                var bytes = value.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String
                    ? EstimateBase64DecodedLength(dataProp.GetString())
                    : 0;
                var isFinal = value.TryGetProperty("isFinal", out var isFinalProp) && isFinalProp.GetBoolean();
                var cached = value.TryGetProperty("cached", out var cachedProp) && cachedProp.GetBoolean();
                var provider = value.TryGetProperty("provider", out var providerProp) ? providerProp.GetString() : null;
                return $"[speech.audio] chunk={chunkIndex} seq={seq} bytes={bytes} provider={provider} cached={cached} final={isFinal}";
            }
        }

        if (string.Equals(streamEvent.Type, "CUSTOM", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamEvent.Name, "speech.audio.error", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(streamEvent.Json);
            if (document.RootElement.TryGetProperty("value", out var value))
            {
                var chunkIndex = value.TryGetProperty("chunkIndex", out var chunkProp) ? chunkProp.GetInt32() : -1;
                var code = value.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "unknown";
                var message = value.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
                return $"[speech.audio.error] chunk={chunkIndex} code={code} {message}";
            }
        }

        return $"[{streamEvent.Type}] {streamEvent.Json}";
    }

    private static int EstimateBase64DecodedLength(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return 0;
        var padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2
            : base64.EndsWith('=') ? 1
            : 0;
        return (base64.Length / 4) * 3 - padding;
    }
}
