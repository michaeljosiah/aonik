using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests;

public class ChatThreadManagerTests
{
    [Fact]
    public async Task EnsureThreadAsync_ShouldReserveNewThreadIdWithoutPersisting_WhenClientDoesNotSupplyGuid()
    {
        // Arrange
        var chatThreadService = new StubChatThreadService();
        var sut = CreateSut(chatThreadService, new InMemoryHistoryCache());
        var messages = new List<AguiMessage>
        {
            new() { Id = "msg-1", Role = "user", Content = "Help me budget better" }
        };

        // Act
        var result = await sut.EnsureThreadAsync(
            clientThreadId: null,
            messages,
            agentId: "personal-finance-agent",
            CancellationToken.None);

        // Assert
        result.IsNewThread.Should().BeTrue();
        result.PersistedThreadId.Should().NotBeNull();
        result.ThreadIdString.Should().Be(result.PersistedThreadId!.Value.ToString("N"));
        result.FirstUserMessage.Should().Be("Help me budget better");
        chatThreadService.CreateThreadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ReconstructHistoryAsync_ShouldUseCacheAfterInitialDatabaseLoad_AndAppendIncomingThinClientTurn()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var chatThreadService = new StubChatThreadService
        {
            ThreadDetail = new ChatThreadDetail
            {
                Id = threadId,
                Title = "Budget thread",
                Status = "Active",
                AgentName = "personal-finance-agent",
                MessageCount = 3,
                CreatedAt = now,
                LastMessageAt = now,
                Messages =
                [
                    new ChatThreadMessageDto
                    {
                        Id = Guid.NewGuid(),
                        Role = "user",
                        Content = "Hello",
                        SortOrder = 1,
                        CreatedAt = now,
                    },
                    new ChatThreadMessageDto
                    {
                        Id = Guid.NewGuid(),
                        Role = "assistant",
                        Content = "Hi there",
                        SortOrder = 2,
                        CreatedAt = now,
                    },
                    new ChatThreadMessageDto
                    {
                        Id = Guid.NewGuid(),
                        Role = "user",
                        Content = "Need a budget",
                        SortOrder = 3,
                        CreatedAt = now,
                    }
                ],
            }
        };
        var historyCache = new InMemoryHistoryCache();
        var sut = CreateSut(chatThreadService, historyCache);

        // Act
        var first = await sut.ReconstructHistoryAsync(
            threadId,
            [new AguiMessage { Id = "turn-1", Role = "user", Content = "Need a budget" }],
            CancellationToken.None);

        var second = await sut.ReconstructHistoryAsync(
            threadId,
            [new AguiMessage { Id = "turn-2", Role = "user", Content = "What should I cut?" }],
            CancellationToken.None);

        // Assert
        first.Source.Should().Be("db");
        first.Messages.Should().NotBeNull();
        first.Messages!.Select(m => m.Content).Should().Equal("Hello", "Hi there", "Need a budget");

        second.Source.Should().Be("cache");
        second.Messages.Should().NotBeNull();
        second.Messages!.Select(m => m.Content).Should().Equal("Hello", "Hi there", "Need a budget", "What should I cut?");

        chatThreadService.GetThreadCallCount.Should().Be(1);
    }

    private static ChatThreadManager CreateSut(
        IChatThreadService chatThreadService,
        IChatThreadHistoryCache historyCache)
    {
        var scopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new ChatThreadManager(
            scopeFactory,
            historyCache,
            NullLogger<ChatThreadManager>.Instance,
            chatThreadService);
    }

    private sealed class StubChatThreadService : IChatThreadService
    {
        public int CreateThreadCallCount { get; private set; }

        public int GetThreadCallCount { get; private set; }

        public ChatThreadDetail? ThreadDetail { get; set; }

        public Task<Guid> CreateThreadAsync(
            string firstMessage,
            string? agentName = null,
            Guid? preferredThreadId = null,
            CancellationToken cancellationToken = default)
        {
            CreateThreadCallCount++;
            return Task.FromResult(preferredThreadId ?? Guid.NewGuid());
        }

        public Task AppendMessageAsync(
            Guid threadId,
            string role,
            string content,
            string? agentName = null,
            Guid? aiRunId = null,
            string? toolCallsJson = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateTitleAsync(
            Guid threadId,
            string title,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ChatThreadDetail?> GetThreadAsync(
            Guid threadId,
            CancellationToken cancellationToken = default)
        {
            GetThreadCallCount++;
            return Task.FromResult(ThreadDetail);
        }

        public Task<List<ChatThreadSummary>> ListThreadsAsync(
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ChatThreadSummary>());

        public Task<bool> ArchiveThreadAsync(
            Guid threadId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class InMemoryHistoryCache : IChatThreadHistoryCache
    {
        private readonly Dictionary<Guid, List<AguiMessage>> _snapshots = new();

        public async Task<ChatThreadHistoryCacheLookup> GetOrLoadAsync(
            Guid threadId,
            Func<CancellationToken, Task<IReadOnlyList<AguiMessage>>> factory,
            CancellationToken cancellationToken = default)
        {
            if (_snapshots.TryGetValue(threadId, out var cached))
            {
                return new ChatThreadHistoryCacheLookup(
                    new ChatThreadHistorySnapshot(CloneMessages(cached)),
                    IsCacheHit: true);
            }

            var loaded = await factory(cancellationToken);
            _snapshots[threadId] = CloneMessages(loaded).ToList();

            return new ChatThreadHistoryCacheLookup(
                new ChatThreadHistorySnapshot(CloneMessages(loaded)),
                IsCacheHit: false);
        }

        public Task StoreAsync(
            Guid threadId,
            IReadOnlyList<AguiMessage> messages,
            CancellationToken cancellationToken = default)
        {
            _snapshots[threadId] = CloneMessages(messages).ToList();
            return Task.CompletedTask;
        }

        public async Task AppendAsync(
            Guid threadId,
            AguiMessage message,
            CancellationToken cancellationToken = default)
        {
            var lookup = await GetOrLoadAsync(
                threadId,
                _ => Task.FromResult<IReadOnlyList<AguiMessage>>([]),
                cancellationToken);

            var updated = lookup.Snapshot.Messages.ToList();
            updated.Add(CloneMessage(message));
            _snapshots[threadId] = updated;
        }

        private static IReadOnlyList<AguiMessage> CloneMessages(IEnumerable<AguiMessage> messages)
            => messages.Select(CloneMessage).ToList();

        private static AguiMessage CloneMessage(AguiMessage message)
            => new()
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                Name = message.Name,
                ToolCallId = message.ToolCallId,
                Error = message.Error,
                EncryptedContent = message.EncryptedContent,
                EncryptedValue = message.EncryptedValue,
                ActivityType = message.ActivityType,
                ToolCalls = message.ToolCalls?.Select(tc => new AguiToolCall
                {
                    Id = tc.Id,
                    Type = tc.Type,
                    EncryptedValue = tc.EncryptedValue,
                    Function = tc.Function is null
                        ? null
                        : new AguiFunctionCall
                        {
                            Name = tc.Function.Name,
                            Arguments = tc.Function.Arguments,
                        }
                }).ToList(),
            };
    }
}
