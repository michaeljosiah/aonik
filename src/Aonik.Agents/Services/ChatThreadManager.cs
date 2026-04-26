using System.Diagnostics;
using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

public sealed class ChatThreadManager : IChatThreadManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatThreadService? _chatThreadService;
    private readonly IChatThreadHistoryCache _historyCache;
    private readonly ITenantContext? _tenantContext;
    private readonly ICurrentUserContext? _userContext;
    private readonly ILogger<ChatThreadManager> _logger;

    public ChatThreadManager(
        IServiceScopeFactory scopeFactory,
        IChatThreadHistoryCache historyCache,
        ILogger<ChatThreadManager> logger,
        IChatThreadService? chatThreadService = null,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? userContext = null)
    {
        _scopeFactory = scopeFactory;
        _historyCache = historyCache;
        _chatThreadService = chatThreadService;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<ChatThreadContext> EnsureThreadAsync(
        string? clientThreadId,
        IReadOnlyList<AguiMessage>? messages,
        string? agentId,
        CancellationToken cancellationToken)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.chat.ensure_thread", ActivityKind.Internal);
        activity?.SetTag("aonik.agent.name", agentId ?? "orchestrator");
        activity?.SetTag("aonik.chat.has_client_thread_id", !string.IsNullOrWhiteSpace(clientThreadId));

        var threadIdString = clientThreadId ?? Guid.NewGuid().ToString("N");

        if (_chatThreadService is null)
        {
            activity?.SetTag("aonik.chat.thread_source", "client");
            return new ChatThreadContext(null, threadIdString, IsNewThread: false, FirstUserMessage: null);
        }

        try
        {
            var firstUserMessage = messages?
                .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                ?.Content;

            if (string.IsNullOrEmpty(firstUserMessage))
            {
                activity?.SetTag("aonik.chat.thread_source", "client");
                activity?.SetTag("aonik.chat.has_user_message", false);
                return new ChatThreadContext(null, threadIdString, IsNewThread: false, FirstUserMessage: null);
            }

            activity?.SetTag("aonik.chat.has_user_message", true);

            if (Guid.TryParse(clientThreadId, out var existingId))
            {
                // Existing thread — append the user message off the critical path.
                EnqueueDetachedUserMessageAppend(existingId, firstUserMessage, agentId);
                activity?.SetTag("aonik.chat.thread_source", "existing");
                activity?.SetTag("aonik.chat.thread_id", existingId.ToString("N"));
                return new ChatThreadContext(existingId, threadIdString, IsNewThread: false, firstUserMessage);
            }

            // New thread — reserve the GUID immediately so SSE can start without
            // waiting on persistence. The coordinator persists the thread after the
            // response is flushed.
            var newId = Guid.NewGuid();
            activity?.SetTag("aonik.chat.thread_source", "reserved");
            activity?.SetTag("aonik.chat.thread_id", newId.ToString("N"));
            return new ChatThreadContext(newId, newId.ToString("N"), IsNewThread: true, firstUserMessage);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogWarning(ex, "AG-UI thread persistence failed — continuing without thread tracking");
            return new ChatThreadContext(null, threadIdString, IsNewThread: false, FirstUserMessage: null);
        }
    }

    public async Task<ChatHistoryResolution> ReconstructHistoryAsync(
        Guid? persistedThreadId,
        IReadOnlyList<AguiMessage>? clientMessages,
        CancellationToken cancellationToken)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.chat.reconstruct_history", ActivityKind.Internal);
        activity?.SetTag("aonik.chat.has_persisted_thread", persistedThreadId.HasValue);
        activity?.SetTag("aonik.chat.client_message_count", clientMessages?.Count ?? 0);

        var stopwatch = Stopwatch.StartNew();

        if (_chatThreadService is null || !persistedThreadId.HasValue)
        {
            activity?.SetTag("aonik.chat.history_source", "client");
            return new ChatHistoryResolution(clientMessages, "client", stopwatch.ElapsedMilliseconds);
        }

        if (clientMessages is null || clientMessages.Count == 0)
        {
            activity?.SetTag("aonik.chat.history_source", "client");
            return new ChatHistoryResolution(clientMessages, "client", stopwatch.ElapsedMilliseconds);
        }

        if (clientMessages.Count != 1)
        {
            await _historyCache.StoreAsync(persistedThreadId.Value, clientMessages, cancellationToken);
            activity?.SetTag("aonik.chat.history_source", "client");
            return new ChatHistoryResolution(clientMessages, "client", stopwatch.ElapsedMilliseconds);
        }

        var only = clientMessages[0];
        if (!string.Equals(only.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            await _historyCache.StoreAsync(persistedThreadId.Value, clientMessages, cancellationToken);
            activity?.SetTag("aonik.chat.history_source", "client");
            return new ChatHistoryResolution(clientMessages, "client", stopwatch.ElapsedMilliseconds);
        }

        try
        {
            var historyLookup = await _historyCache.GetOrLoadAsync(
                persistedThreadId.Value,
                async ct =>
                {
                    var detail = await _chatThreadService.GetThreadAsync(persistedThreadId.Value, ct);
                    if (detail is null || detail.Messages.Count == 0)
                        return [];

                    return detail.Messages
                        .OrderBy(m => m.SortOrder)
                        .Select(m => new AguiMessage
                        {
                            Id = m.Id.ToString("N"),
                            Role = m.Role,
                            Content = m.Content,
                        })
                        .ToList();
                },
                cancellationToken);

            var reconstructed = historyLookup.Snapshot.Messages.ToList();

            // Trim a trailing duplicate user message (the incoming turn may already
            // have been appended via the detached path for existing threads, or
            // inline via CreateThreadAsync for new threads).
            for (var i = reconstructed.Count - 1; i >= 0; i--)
            {
                var tail = reconstructed[i];
                if (string.Equals(tail.Role, "user", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tail.Content, only.Content, StringComparison.Ordinal))
                {
                    reconstructed.RemoveAt(i);
                    break;
                }

                if (!string.Equals(tail.Role, "user", StringComparison.OrdinalIgnoreCase))
                    break;
            }

            reconstructed.Add(only);
            await _historyCache.StoreAsync(persistedThreadId.Value, reconstructed, cancellationToken);

            var source = historyLookup.IsCacheHit ? "cache" : "db";
            activity?.SetTag("aonik.chat.history_source", source);
            activity?.SetTag("aonik.chat.history_message_count", reconstructed.Count);

            return new ChatHistoryResolution(
                reconstructed,
                source,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogWarning(ex,
                "AG-UI thin-client history reconstruction failed for thread {ThreadId} — falling back to client-supplied messages",
                persistedThreadId.Value);
            return new ChatHistoryResolution(clientMessages, "client", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            activity?.SetTag("aonik.chat.history_duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private void EnqueueDetachedUserMessageAppend(Guid threadId, string content, string? agentName)
    {
        var capturedTenantId = _tenantContext?.TenantId;
        var capturedUserId = _userContext?.UserId;
        var scopeFactory = _scopeFactory;
        var logger = _logger;

        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = scopeFactory.CreateScope();
                var bgServices = bgScope.ServiceProvider;

                SeedBackgroundContext(bgServices, capturedTenantId, capturedUserId, "agui-user-append");

                var bgChatThreadService = bgServices.GetService<IChatThreadService>();
                if (bgChatThreadService is null) return;

                await bgChatThreadService.AppendMessageAsync(
                    threadId, "user", content,
                    agentName: agentName,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AG-UI detached user-message append failed for thread {ThreadId}", threadId);
            }
        });
    }

    internal static void SeedBackgroundContext(
        IServiceProvider bgServices,
        Guid? tenantId,
        Guid? userId,
        string resolutionSource)
    {
        if (tenantId.HasValue)
        {
            var tc = bgServices.GetService<ITenantContext>();
            if (tc is not null)
            {
                tc.TenantId = tenantId.Value;
                tc.ResolutionSource = resolutionSource;
            }
        }

        if (userId.HasValue)
        {
            var uc = bgServices.GetService<ICurrentUserContext>();
            if (uc is not null)
            {
                uc.UserId = userId.Value;
                uc.TenantId = tenantId;
                uc.IsAuthenticated = true;
            }
        }
    }
}
