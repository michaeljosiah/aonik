using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

public sealed class AgentCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public AgentCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var agents = await _apiClient.ListAgentsAsync(session, cancellationToken);
        await _outputWriter.WriteAgentsAsync(agents, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> RunAsync(RunAgentOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Message))
        {
            throw new AonikCliException("'--message' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var request = new ChatRequest(
            options.Message,
            options.SessionId ?? session.LastSessionId,
            options.ThreadId ?? session.LastThreadId);

        var response = await _apiClient.ChatAsync(session, request, cancellationToken);

        var updatedSession = session with
        {
            LastSessionId = response.SessionId,
            LastThreadId = response.ThreadId ?? session.LastThreadId
        };

        await _sessionStore.SaveAsync(updatedSession, cancellationToken);
        await _outputWriter.WriteChatResponseAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> StreamAsync(StreamAgentOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Message))
        {
            throw new AonikCliException("'--message' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        string? lastThreadId = session.LastThreadId;
        string? lastSessionId = session.LastSessionId;

        await _apiClient.StreamAgentAsync(
            session,
            new AgentStreamRequest(
                options.Message,
                options.ThreadId ?? session.LastThreadId,
                options.RunId,
                options.AgentId),
            async streamEvent =>
            {
                UpdateSessionTracking(streamEvent, ref lastThreadId, ref lastSessionId);
                await _outputWriter.WriteStreamEventAsync(streamEvent, options.OutputMode, cancellationToken);
            },
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(lastThreadId) || !string.IsNullOrWhiteSpace(lastSessionId))
        {
            await _sessionStore.SaveAsync(
                session with
                {
                    LastThreadId = lastThreadId,
                    LastSessionId = lastSessionId
                },
                cancellationToken);
        }

        return 0;
    }

    public async Task<int> ListThreadsAsync(ListThreadsOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var threads = await _apiClient.ListThreadsAsync(session, options.Page, options.PageSize, cancellationToken);
        await _outputWriter.WriteThreadsAsync(threads, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetThreadAsync(Guid threadId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var thread = await _apiClient.GetThreadAsync(session, threadId, cancellationToken);
        await _outputWriter.WriteThreadAsync(thread, outputMode, cancellationToken);
        return 0;
    }

    private async Task<CliSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            throw new AonikCliException("No active session found. Run 'aonik auth login' first.");
        }

        return session;
    }

    private static void UpdateSessionTracking(AgentStreamEvent streamEvent, ref string? threadId, ref string? sessionId)
    {
        using var document = JsonDocument.Parse(streamEvent.Json);
        var root = document.RootElement;

        if (root.TryGetProperty("threadId", out var threadIdElement))
        {
            threadId = threadIdElement.GetString() ?? threadId;
        }

        if (root.TryGetProperty("runId", out var runIdElement))
        {
            sessionId = runIdElement.GetString() ?? sessionId;
        }
    }
}
