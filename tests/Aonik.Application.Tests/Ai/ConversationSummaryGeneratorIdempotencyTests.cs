using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Regression tests for the runaway OpenAI spend bug discovered on 2026-04-14.
///
/// The conversation summary generator was re-processing the same chat thread on
/// every 5-minute stale-session sweep when the LLM call (or anything downstream)
/// threw — because the only "done" signal was the persisted ConversationSummary
/// row, which never got written on failure. A single broken thread could therefore
/// re-bill OpenAI every 5 minutes indefinitely.
///
/// See: src/Aonik.Agents/Services/ConversationSummaryGenerator.cs
/// </summary>
public class ConversationSummaryGeneratorIdempotencyTests
{
    private static readonly Guid TenantId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("81000000-0000-0000-0000-000000000001");
    private const string AgentName = "personal-finance-agent";

    [Fact]
    public async Task ProcessStaleSessions_ShouldStopReprocessingThread_AfterMaxAttempts()
    {
        var chatClient = new ThrowingChatClient();
        await using var dbContext = CreateDbContext();
        var threadId = SeedStaleThread(dbContext);

        var generator = new ConversationSummaryGenerator(
            dbContext,
            chatClient,
            new StaticTaskProfileResolver(),
            NullLogger<ConversationSummaryGenerator>.Instance,
            new RecordingMemoryProvider());

        // Simulate the cron firing every 5 minutes for an hour (12 sweeps).
        for (var i = 0; i < 12; i++)
        {
            await generator.ProcessStaleSessionsAsync(10, [AgentName]);
        }

        // Each attempt makes up to 2 chat calls (summary + memory extraction). The
        // critical assertion is that total calls are BOUNDED by the attempt cap —
        // not unbounded (which would be 12 sweeps × 2 = 24 calls without the fix).
        chatClient.CallCount.Should().BeLessThanOrEqualTo(ConversationSummaryGenerator.MaxSummaryAttempts * 2);

        // The thread must be parked — attempt counter at the cap.
        var thread = await dbContext.ChatThreads.AsNoTracking().FirstAsync(t => t.Id == threadId);
        thread.SummaryAttemptCount.Should().Be(ConversationSummaryGenerator.MaxSummaryAttempts);
        thread.SummaryLastAttemptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessStaleSessions_ShouldOnlyCallChatClientOnce_WhenSummarySucceeds()
    {
        var chatClient = new SuccessfulChatClient();
        await using var dbContext = CreateDbContext();
        var threadId = SeedStaleThread(dbContext);

        var generator = new ConversationSummaryGenerator(
            dbContext,
            chatClient,
            new StaticTaskProfileResolver(),
            NullLogger<ConversationSummaryGenerator>.Instance);

        await generator.ProcessStaleSessionsAsync(10, [AgentName]);
        await generator.ProcessStaleSessionsAsync(10, [AgentName]);
        await generator.ProcessStaleSessionsAsync(10, [AgentName]);

        // Once a ConversationSummary row exists, the stale query must exclude the
        // thread, so the chat client is called exactly once across all sweeps.
        chatClient.CallCount.Should().Be(1);

        var summaryCount = await dbContext.ConversationSummaries
            .CountAsync(s => s.ChatThreadId == threadId);
        summaryCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessStaleSessions_ShouldCapMemoriesPerSession_WhenLlmReturnsTooMany()
    {
        // 50 memories returned by the LLM — must be truncated to MaxMemoriesPerSession.
        var chatClient = new MemoryFloodChatClient(memoryCount: 50);
        var memoryProvider = new RecordingMemoryProvider();
        await using var dbContext = CreateDbContext();
        SeedStaleThread(dbContext);

        var generator = new ConversationSummaryGenerator(
            dbContext,
            chatClient,
            new StaticTaskProfileResolver(),
            NullLogger<ConversationSummaryGenerator>.Instance,
            memoryProvider);

        await generator.ProcessStaleSessionsAsync(10, [AgentName]);

        memoryProvider.SaveCount.Should().Be(ConversationSummaryGenerator.MaxMemoriesPerSession);
    }

    private static AgentsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"ConversationSummary_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new StaticTenantProvider(TenantId);
        var currentUserProvider = new StaticCurrentUserProvider(UserId);
        return new AgentsDbContext(options, tenantProvider, currentUserProvider, new SystemClock());
    }

    private static Guid SeedStaleThread(AgentsDbContext dbContext)
    {
        var threadId = Guid.NewGuid();
        var thread = new ChatThread
        {
            Id = threadId,
            TenantId = TenantId,
            UserId = UserId,
            Title = "Test session",
            Status = ChatThreadStatus.Active,
            AgentName = AgentName,
            LastMessageAt = DateTime.UtcNow.AddHours(-1),
            MessageCount = 2
        };
        thread.Messages.Add(new ChatThreadMessage
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ChatThreadId = threadId,
            Role = "user",
            Content = "What's my balance?",
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        thread.Messages.Add(new ChatThreadMessage
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ChatThreadId = threadId,
            Role = "assistant",
            Content = "£500.",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        dbContext.ChatThreads.Add(thread);
        dbContext.SaveChanges();
        return threadId;
    }

    // ── Test doubles ────────────────────────────────────────────────────────

    private sealed class StaticTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public StaticTenantProvider(Guid tenantId) { _tenantId = tenantId; }
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class StaticCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;
        public StaticCurrentUserProvider(Guid userId) { _userId = userId; }
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _userId; return true; }
    }

    private sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class StaticTaskProfileResolver : IAiTaskProfileResolver
    {
        public Task<AiTaskProfile> ResolveAsync(
            string useCase,
            string? promptName = null,
            string? defaultModelId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AiTaskProfile("test-model", "system", null));
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public int CallCount { get; private set; }
        public ChatClientMetadata Metadata { get; } = new("ThrowingChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Simulated upstream LLM failure.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class SuccessfulChatClient : IChatClient
    {
        public int CallCount { get; private set; }
        public ChatClientMetadata Metadata { get; } = new("SuccessfulChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            const string responseText = """{"summary":"Discussed account balance."}""";
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class MemoryFloodChatClient : IChatClient
    {
        private readonly int _memoryCount;
        public ChatClientMetadata Metadata { get; } = new("MemoryFloodChatClient");

        public MemoryFloodChatClient(int memoryCount) { _memoryCount = memoryCount; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            // The first call is the summary (returns a summary); the second is memory extraction.
            // We can't easily distinguish, so we return both shapes — the parsers ignore the wrong shape.
            var memoryItems = string.Join(",", Enumerable.Range(0, _memoryCount).Select(i =>
                $$"""{"key":"k{{i}}","entryType":"Fact","valueJson":"\"v{{i}}\"","confidence":0.8}"""));
            var responseText = $$"""
            {
              "summary":"x",
              "memories":[{{memoryItems}}]
            }
            """;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class RecordingMemoryProvider : IUserMemorySaveProvider
    {
        public int SaveCount { get; private set; }

        public Task<UserMemorySaveResult> SaveAsync(
            UserMemorySaveRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(new UserMemorySaveResult(Guid.NewGuid(), request.Key, false));
        }
    }
}
