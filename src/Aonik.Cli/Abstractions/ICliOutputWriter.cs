using Aonik.Cli.Models;

namespace Aonik.Cli.Abstractions;

public interface ICliOutputWriter
{
    Task WriteInfoAsync(string message, CancellationToken cancellationToken = default);

    Task WriteObjectAsync<T>(T value, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteCollectionAsync<T>(IReadOnlyList<T> values, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteSessionAsync(CliSession session, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteUserInfoAsync(UserInfoResponseDto userInfo, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteAgentsAsync(IReadOnlyList<AgentInfo> agents, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteChatResponseAsync(AgentChatResponse response, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteThreadsAsync(IReadOnlyList<ChatThreadSummary> threads, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteThreadAsync(ChatThreadDetail thread, OutputMode outputMode, CancellationToken cancellationToken = default);

    Task WriteStreamEventAsync(AgentStreamEvent streamEvent, OutputMode outputMode, CancellationToken cancellationToken = default);
}
