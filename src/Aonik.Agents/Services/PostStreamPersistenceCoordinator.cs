using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

public sealed class PostStreamPersistenceCoordinator : IPostStreamPersistenceCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostStreamPersistenceCoordinator> _logger;

    public PostStreamPersistenceCoordinator(
        IServiceScopeFactory scopeFactory,
        ILogger<PostStreamPersistenceCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(PostStreamPersistenceContext context)
    {
        var scopeFactory = _scopeFactory;
        var logger = _logger;

        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = scopeFactory.CreateScope();
                var bgServices = bgScope.ServiceProvider;

                ChatThreadManager.SeedBackgroundContext(
                    bgServices, context.TenantId, context.UserId, "agui-post-stream");

                var bgLogger = bgServices.GetRequiredService<ILogger<PostStreamPersistenceCoordinator>>();

                await PersistThreadAsync(bgServices, bgLogger, context);
                await PersistAiRunAsync(bgServices, bgLogger, context);
            }
            catch (Exception bgEx)
            {
                logger.LogWarning(bgEx,
                    "AG-UI post-stream background task crashed for thread {ThreadId}",
                    context.ThreadIdString);
            }
        });
    }

    private static async Task PersistThreadAsync(
        IServiceProvider bgServices,
        ILogger logger,
        PostStreamPersistenceContext context)
    {
        var chatThreadService = bgServices.GetService<IChatThreadService>();
        var historyCache = bgServices.GetService<IChatThreadHistoryCache>();
        if (chatThreadService is null || !context.PersistedThreadId.HasValue)
            return;

        var threadId = context.PersistedThreadId.Value;

        try
        {
            if (context.IsNewThread && !string.IsNullOrEmpty(context.FirstUserMessage))
            {
                await chatThreadService.CreateThreadAsync(
                    context.FirstUserMessage,
                    agentName: context.AgentId,
                    preferredThreadId: threadId,
                    cancellationToken: CancellationToken.None);
            }

            if (!string.IsNullOrEmpty(context.AssistantText))
            {
                await chatThreadService.AppendMessageAsync(
                    threadId,
                    "assistant",
                    context.AssistantText,
                    agentName: context.AgentId,
                    cancellationToken: CancellationToken.None);

                if (historyCache is not null)
                {
                    // Cache key is tenant-prefixed. context.TenantId is
                    // captured by the AGUI endpoint at request time and
                    // re-seeded into the background scope upstream;
                    // Guid.Empty is the documented fallback for the rare
                    // background-only paths where no tenant was bound.
                    await historyCache.AppendAsync(
                        context.TenantId ?? Guid.Empty,
                        threadId,
                        new AguiMessage
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Role = "assistant",
                            Content = context.AssistantText,
                        },
                        CancellationToken.None);
                }
            }

            if (context.IsNewThread && !string.IsNullOrEmpty(context.FirstUserMessage))
            {
                var titleGenerator = bgServices.GetService<IChatThreadTitleGenerator>();
                if (titleGenerator is null) return;

                try
                {
                    var title = await titleGenerator.GenerateTitleAsync(
                        context.FirstUserMessage, CancellationToken.None);
                    await chatThreadService.UpdateTitleAsync(threadId, title, CancellationToken.None);
                }
                catch (Exception titleEx)
                {
                    logger.LogWarning(titleEx,
                        "AG-UI title generation failed for thread {ThreadId} — placeholder title retained",
                        threadId);
                }
            }
        }
        catch (Exception persistEx)
        {
            logger.LogWarning(persistEx,
                "AG-UI post-stream persistence failed for thread {ThreadId}", threadId);
        }
    }

    private static async Task PersistAiRunAsync(
        IServiceProvider bgServices,
        ILogger logger,
        PostStreamPersistenceContext context)
    {
        var aiRunWriter = bgServices.GetService<IAiRunWriter>();
        if (aiRunWriter is null) return;

        try
        {
            // Voice (and any future caller) can specify UseCase explicitly so AiRun
            // rows aren't tagged by AgentId. AGUI doesn't supply it and falls back
            // to the legacy AgentId-derived behavior.
            var useCase = !string.IsNullOrWhiteSpace(context.UseCase)
                ? context.UseCase!
                : context.AgentId ?? "master-orchestrator";
            var aiRunId = await aiRunWriter.StartRunAsync(
                useCase,
                $"{{\"threadId\":\"{context.ThreadIdString}\"}}",
                CancellationToken.None);

            var totalTokens = context.InputTokens + context.OutputTokens;
            await aiRunWriter.MarkRunCompletedWithMetricsAsync(
                aiRunId,
                tokensUsed: (int)totalTokens,
                latencyMs: (int)context.LatencyMs,
                costEstimate: 0m,
                outputRef: $"tokens:{totalTokens},latency:{context.LatencyMs}ms",
                cancellationToken: CancellationToken.None);
        }
        catch (Exception aiRunEx)
        {
            logger.LogWarning(aiRunEx,
                "AG-UI post-stream AiRun persistence failed for run {RunId}", context.RunId);
        }
    }
}
