using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalFinanceNarrativeInsightsServiceTests
{
    [Fact]
    public async Task GenerateSpendingNarrativeAsync_ShouldMarkRunFailed_WhenChatClientThrows()
    {
        // Arrange
        var runWriter = new FakeAiRunWriter();
        var insightWriter = new FakeInsightWriter();
        var service = new PersonalFinanceNarrativeInsightsService(
            new FakeInsightsService(),
            new FakePromptStore(),
            new ThrowingChatClient(),
            insightWriter,
            runWriter);

        var request = new GeneratePersonalSpendingNarrativeRequest(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null);

        // Act
        Func<Task> action = async () => await service.GenerateSpendingNarrativeAsync(request);

        // Assert
        await action.Should().ThrowAsync<TimeoutException>();
        runWriter.StartedRuns.Should().HaveCount(1);
        runWriter.FailedRuns.Should().HaveCount(1);
        runWriter.FailedRuns[0].RunId.Should().Be(runWriter.StartedRuns[0]);
        runWriter.FailedRuns[0].Reason.Should().Contain("timed out");
        runWriter.CompletedRuns.Should().BeEmpty();
        insightWriter.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task GenerateSpendingNarrativeAsync_ShouldMarkRunCompleted_WhenChatClientSucceeds()
    {
        // Arrange
        var runWriter = new FakeAiRunWriter();
        var service = new PersonalFinanceNarrativeInsightsService(
            new FakeInsightsService(),
            new FakePromptStore(),
            new SuccessfulChatClient(),
            new FakeInsightWriter(),
            runWriter);

        var request = new GeneratePersonalSpendingNarrativeRequest(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null);

        // Act
        var response = await service.GenerateSpendingNarrativeAsync(request);

        // Assert
        response.AiRunId.Should().Be(runWriter.StartedRuns[0]);
        runWriter.StartedRuns.Should().HaveCount(1);
        runWriter.CompletedRuns.Should().HaveCount(1);
        runWriter.CompletedRuns[0].RunId.Should().Be(runWriter.StartedRuns[0]);
        runWriter.CompletedRuns[0].OutputRef.Should().StartWith("insight:");
        runWriter.FailedRuns.Should().BeEmpty();
    }

    private sealed class FakeInsightsService : IPersonalFinanceInsightsService
    {
        public Task<SpendingSummaryResponse> GetSpendingSummaryAsync(
            DateTime periodStart,
            DateTime periodEnd,
            Guid? personalAccountId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpendingSummaryResponse(
                periodStart,
                periodEnd,
                "USD",
                2000m,
                500m,
                1500m,
                8));
        }

        public Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdownAsync(
            DateTime periodStart,
            DateTime periodEnd,
            Guid? personalAccountId = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CategorySpendingItemResponse> response =
            [
                new CategorySpendingItemResponse("Groceries", 250m, 50m, 3),
                new CategorySpendingItemResponse("Transport", 100m, 20m, 2)
            ];

            return Task.FromResult(response);
        }

        public Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdownAsync(
            DateTime periodStart,
            DateTime periodEnd,
            Guid? personalAccountId = null,
            int top = 10,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MerchantSpendingItemResponse> response =
            [
                new MerchantSpendingItemResponse("Store A", 150m, 2),
                new MerchantSpendingItemResponse("Store B", 100m, 1)
            ];

            return Task.FromResult(response);
        }

        public Task<IReadOnlyList<AccountSpendingItemResponse>> GetAccountBreakdownAsync(
            DateTime periodStart,
            DateTime periodEnd,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountSpendingItemResponse> response =
            [
                new AccountSpendingItemResponse(Guid.NewGuid(), "Main Account", -500m, 8)
            ];

            return Task.FromResult(response);
        }
    }

    private sealed class FakePromptStore : IPromptStore
    {
        public Task<string> LoadPromptAsync(
            string promptName,
            string version = "v1",
            string role = "system",
            CancellationToken cancellationToken = default)
        {
            var prompt = role == "system"
                ? "You are a spending analyst."
                : "Input data: {{SPENDING_DATA}}";

            return Task.FromResult(prompt);
        }
    }

    private sealed class FakeInsightWriter : IInsightWriter
    {
        public int SaveCount { get; private set; }

        public Task<InsightResponse> SaveInsightAsync(
            string subjectType,
            Guid subjectId,
            string title,
            string summary,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;

            return Task.FromResult(new InsightResponse(
                Guid.NewGuid(),
                subjectType,
                subjectId,
                title,
                summary,
                DateTime.UtcNow));
        }
    }

    private sealed class FakeAiRunWriter : IAiRunWriter
    {
        public List<Guid> StartedRuns { get; } = new();
        public List<(Guid RunId, string? OutputRef)> CompletedRuns { get; } = new();
        public List<(Guid RunId, string Reason)> FailedRuns { get; } = new();

        public Task<Guid> StartRunAsync(
            string useCase,
            string inputRefsJson,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            StartedRuns.Add(id);
            return Task.FromResult(id);
        }

        public Task MarkRunCompletedAsync(
            Guid aiRunId,
            string? outputRef = null,
            CancellationToken cancellationToken = default)
        {
            CompletedRuns.Add((aiRunId, outputRef));
            return Task.CompletedTask;
        }

        public Task MarkRunFailedAsync(
            Guid aiRunId,
            string failureReason,
            CancellationToken cancellationToken = default)
        {
            FailedRuns.Add((aiRunId, failureReason));
            return Task.CompletedTask;
        }

        public async Task<Guid> SaveRunAsync(
            string useCase,
            string inputRefsJson,
            string outcome,
            CancellationToken cancellationToken = default)
        {
            var id = await StartRunAsync(useCase, inputRefsJson, cancellationToken);

            if (string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                await MarkRunFailedAsync(id, "Failed", cancellationToken);
                return id;
            }

            if (!string.Equals(outcome, "Started", StringComparison.OrdinalIgnoreCase))
            {
                await MarkRunCompletedAsync(id, null, cancellationToken);
            }

            return id;
        }
    }

    private sealed class SuccessfulChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("SuccessfulChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "Insight narrative") ]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(IChatClient))
            {
                return this;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("ThrowingChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("Provider timed out.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(IChatClient))
            {
                return this;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
