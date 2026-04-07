using System.Text.Json;

using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

public class CustomerInsightAiSummaryServiceTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider : ITenantProvider
    {
        private readonly ITenantContext _tenantContext;

        public ContextTenantProvider(ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
        }

        public Guid GetCurrentTenantId() => _tenantContext.TenantId ?? Guid.Empty;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantContext.TenantId ?? Guid.Empty;
            return _tenantContext.TenantId.HasValue;
        }
    }

    private sealed class StaticCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid? _userId;

        public StaticCurrentUserProvider(Guid? userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId ?? Guid.Empty;
            return _userId.HasValue;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    private sealed class FakeTaskProfileResolver : IAiTaskProfileResolver
    {
        public string? ModelId { get; set; } = "model-a";

        public Task<AiTaskProfile> ResolveAsync(
            string useCase,
            string? promptName = null,
            string? defaultModelId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiTaskProfile(
                ModelId,
                "You are a grounded customer insight summarizer.",
                "{{SNAPSHOT_JSON}}"));
        }
    }

    private sealed class QueueChatClient : IChatClient
    {
        private readonly Queue<Func<Task<ChatResponse>>> _responses = new();

        public ChatClientMetadata Metadata { get; } = new("QueueChatClient");

        public void EnqueueText(string responseText)
        {
            _responses.Enqueue(() => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)])));
        }

        public void EnqueueException(Exception exception)
        {
            _responses.Enqueue(() => Task.FromException<ChatResponse>(exception));
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued chat response was available.");
            }

            return _responses.Dequeue().Invoke();
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

    private static FinanceDbContext CreateFinanceDbContext(ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"CustomerInsightAiSummary_Finance_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, tenantProvider);
    }

    private static AiDbContext CreateAiDbContext(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"CustomerInsightAiSummary_Ai_{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, tenantProvider, currentUserProvider, clock);
    }

    [Fact]
    public async Task GenerateCurrentSummaryAsync_ShouldPersistCurrentSummaryWithAiRunLink()
    {
        // Arrange
        var tenantId = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var userId = Guid.Parse("52000000-0000-0000-0000-000000000001");
        var tenantContext = new TestTenantContext { TenantId = tenantId, ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var currentUserProvider = new StaticCurrentUserProvider(userId);

        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var snapshotId = SeedCurrentSnapshot(financeDbContext, tenantId, userId, clock.UtcNow);
        var snapshotReader = new CustomerInsightSnapshotReader(financeDbContext);
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var profileResolver = new FakeTaskProfileResolver { ModelId = "model-a" };
        var chatClient = new QueueChatClient();
        chatClient.EnqueueText(BuildValidSummaryJson("Cash is stable", "Spending pressure is manageable."));
        var aiRunWriter = new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider);
        var service = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            profileResolver,
            chatClient,
            aiRunWriter,
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);

        // Act
        var result = await service.GenerateCurrentSummaryAsync(snapshotId);

        // Assert
        result.Status.Should().Be(CustomerInsightAiSummaryContract.StatusCurrent);
        result.CustomerInsightSnapshotId.Should().Be(snapshotId);
        result.AiRunId.Should().NotBeEmpty();
        result.NarrativeVersion.Should().Be(CustomerInsightAiSummaryContract.BuildNarrativeVersion("model-a"));
        result.Summary.Should().NotBeNull();
        result.Summary!.Headline.Should().Be("Cash is stable");
        result.Summary.ReferencedMetrics.Should().Contain("metrics.cashPosition.totalBalanceByCurrency");

        var persisted = await aiDbContext.CustomerInsightAiSummaries.SingleAsync();
        persisted.AiRunId.Should().Be(result.AiRunId);
        persisted.CustomerInsightSnapshotId.Should().Be(snapshotId);

        var aiRun = await aiDbContext.AiRuns.SingleAsync();
        aiRun.Id.Should().Be(result.AiRunId);
        aiRun.Outcome.Should().Be("Completed");
        aiRun.OutputRef.Should().StartWith("customer-insight-ai-summary:");
    }

    [Fact]
    public async Task GenerateCurrentSummaryAsync_ShouldReuseCurrentSummary_WhenNarrativeVersionIsUnchanged()
    {
        // Arrange
        var tenantId = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var userId = Guid.Parse("52000000-0000-0000-0000-000000000002");
        var tenantContext = new TestTenantContext { TenantId = tenantId, ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var currentUserProvider = new StaticCurrentUserProvider(userId);

        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var snapshotId = SeedCurrentSnapshot(financeDbContext, tenantId, userId, clock.UtcNow);
        var snapshotReader = new CustomerInsightSnapshotReader(financeDbContext);
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var profileResolver = new FakeTaskProfileResolver { ModelId = "model-a" };
        var chatClient = new QueueChatClient();
        chatClient.EnqueueText(BuildValidSummaryJson("Cash is stable", "Spending pressure is manageable."));
        var aiRunWriter = new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider);
        var service = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            profileResolver,
            chatClient,
            aiRunWriter,
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);

        // Act
        var first = await service.GenerateCurrentSummaryAsync(snapshotId);
        var second = await service.GenerateCurrentSummaryAsync(snapshotId);

        // Assert
        second.Id.Should().Be(first.Id);
        (await aiDbContext.CustomerInsightAiSummaries.CountAsync()).Should().Be(1);
        (await aiDbContext.AiRuns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GenerateCurrentSummaryAsync_ShouldSupersedeCurrentSummary_WhenNarrativeVersionChanges()
    {
        // Arrange
        var tenantId = Guid.Parse("51000000-0000-0000-0000-000000000003");
        var userId = Guid.Parse("52000000-0000-0000-0000-000000000003");
        var tenantContext = new TestTenantContext { TenantId = tenantId, ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var currentUserProvider = new StaticCurrentUserProvider(userId);

        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var snapshotId = SeedCurrentSnapshot(financeDbContext, tenantId, userId, clock.UtcNow);
        var snapshotReader = new CustomerInsightSnapshotReader(financeDbContext);
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var profileResolver = new FakeTaskProfileResolver { ModelId = "model-a" };
        var chatClient = new QueueChatClient();
        chatClient.EnqueueText(BuildValidSummaryJson("Cash is stable", "Spending pressure is manageable."));
        chatClient.EnqueueText(BuildValidSummaryJson("Spending risk increased", "Entertainment spend is rising."));
        var aiRunWriter = new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider);
        var service = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            profileResolver,
            chatClient,
            aiRunWriter,
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);

        var first = await service.GenerateCurrentSummaryAsync(snapshotId);
        profileResolver.ModelId = "model-b";

        // Act
        var second = await service.GenerateCurrentSummaryAsync(snapshotId);

        // Assert
        second.Id.Should().NotBe(first.Id);
        second.NarrativeVersion.Should().Be(CustomerInsightAiSummaryContract.BuildNarrativeVersion("model-b"));

        var summaries = await aiDbContext.CustomerInsightAiSummaries
            .IgnoreQueryFilters()
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        summaries.Should().HaveCount(2);
        summaries[0].Status.Should().Be(CustomerInsightAiSummaryContract.StatusSuperseded);
        summaries[0].SupersededById.Should().Be(summaries[1].Id);
        summaries[1].Status.Should().Be(CustomerInsightAiSummaryContract.StatusCurrent);
    }

    [Fact]
    public async Task GenerateCurrentSummaryAsync_ShouldPersistFailedSummary_WhenChatClientThrows()
    {
        // Arrange
        var tenantId = Guid.Parse("51000000-0000-0000-0000-000000000004");
        var userId = Guid.Parse("52000000-0000-0000-0000-000000000004");
        var tenantContext = new TestTenantContext { TenantId = tenantId, ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var currentUserProvider = new StaticCurrentUserProvider(userId);

        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var snapshotId = SeedCurrentSnapshot(financeDbContext, tenantId, userId, clock.UtcNow);
        var snapshotReader = new CustomerInsightSnapshotReader(financeDbContext);
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var profileResolver = new FakeTaskProfileResolver();
        var chatClient = new QueueChatClient();
        chatClient.EnqueueException(new TimeoutException("Provider timed out."));
        var aiRunWriter = new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider);
        var service = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            profileResolver,
            chatClient,
            aiRunWriter,
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);

        // Act
        var result = await service.GenerateCurrentSummaryAsync(snapshotId);

        // Assert
        result.Status.Should().Be(CustomerInsightAiSummaryContract.StatusFailed);
        result.FailureReason.Should().Contain("timed out");
        result.Summary.Should().BeNull();

        var failed = await aiDbContext.CustomerInsightAiSummaries.SingleAsync();
        failed.Status.Should().Be(CustomerInsightAiSummaryContract.StatusFailed);

        var aiRun = await aiDbContext.AiRuns.SingleAsync();
        aiRun.Outcome.Should().Be("Failed");
        aiRun.OutputRef.Should().Be("Provider timed out.");
    }

    [Fact]
    public async Task GenerateCurrentSummaryAsync_ShouldPersistFailedSummary_WhenResponseFailsSchemaValidation()
    {
        // Arrange
        var tenantId = Guid.Parse("51000000-0000-0000-0000-000000000005");
        var userId = Guid.Parse("52000000-0000-0000-0000-000000000005");
        var tenantContext = new TestTenantContext { TenantId = tenantId, ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var currentUserProvider = new StaticCurrentUserProvider(userId);

        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var snapshotId = SeedCurrentSnapshot(financeDbContext, tenantId, userId, clock.UtcNow);
        var snapshotReader = new CustomerInsightSnapshotReader(financeDbContext);
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var profileResolver = new FakeTaskProfileResolver();
        var chatClient = new QueueChatClient();
        chatClient.EnqueueText("{\"summary\":\"missing required fields\"}");
        var aiRunWriter = new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider);
        var service = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            profileResolver,
            chatClient,
            aiRunWriter,
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);

        // Act
        var result = await service.GenerateCurrentSummaryAsync(snapshotId);

        // Assert
        result.Status.Should().Be(CustomerInsightAiSummaryContract.StatusFailed);
        result.FailureReason.Should().Contain("schema validation failed");

        var aiRun = await aiDbContext.AiRuns.SingleAsync();
        aiRun.Outcome.Should().Be("Failed");
    }

    private static Guid SeedCurrentSnapshot(FinanceDbContext dbContext, Guid tenantId, Guid userId, DateTime asOfUtc)
    {
        var snapshotId = Guid.NewGuid();
        var document = new CustomerInsightSnapshotDocument(
            CustomerInsightSnapshotContract.SchemaVersion,
            userId,
            tenantId,
            asOfUtc,
            new CustomerInsightAnalysisWindow(
                asOfUtc.AddDays(-CustomerInsightSnapshotContract.BehaviourWindowDays),
                asOfUtc,
                CustomerInsightSnapshotContract.OperationalWindowDays,
                CustomerInsightSnapshotContract.TrendWindowDays,
                CustomerInsightSnapshotContract.BehaviourWindowDays,
                CustomerInsightSnapshotContract.ObligationsLookaheadDays),
            new CustomerInsightCurrencyPolicy(
                CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency,
                null,
                null,
                CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers),
            ["USD"],
            new CustomerInsightCoverage(false, ["accounts", "transactions"], [], [], []),
            new CustomerInsightMetrics(
                new CustomerInsightCashPosition(
                    1,
                    [new CustomerInsightMoneyAmount("USD", 1800m)],
                    [new CustomerInsightMoneyAmount("USD", 1800m)],
                    [new CustomerInsightAccountBalance(Guid.NewGuid(), "Main Wallet", "Bank", "USD", 1800m, 100m)],
                    [new CustomerInsightConcentrationRatio("USD", 100m)]),
                new CustomerInsightIncomeSummary(
                    30,
                    asOfUtc.AddDays(-30),
                    asOfUtc,
                    [new CustomerInsightMoneyAmount("USD", 3000m)],
                    [new CustomerInsightMoneyAmount("USD", 3000m)],
                    "monthly",
                    [new CustomerInsightSourceAmount("Employer Inc", "USD", 3000m, 1)],
                    [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main Wallet", "USD", 3000m, 1)],
                    [new CustomerInsightPeriodDelta("USD", 3000m, 2900m, 100m, 3.45m)]),
                new CustomerInsightExpenseSummary(
                    30,
                    asOfUtc.AddDays(-30),
                    asOfUtc,
                    [new CustomerInsightMoneyAmount("USD", 1600m)],
                    [new CustomerInsightMoneyAmount("USD", 1100m)],
                    [new CustomerInsightMoneyAmount("USD", 500m)],
                    [new CustomerInsightMoneyAmount("USD", 1200m)],
                    [new CustomerInsightMoneyAmount("USD", 400m)],
                    [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main Wallet", "USD", 1600m, 4)],
                    [new CustomerInsightPeriodDelta("USD", 1600m, 1400m, 200m, 14.29m)],
                    [new CustomerInsightAverageSpend("USD", 373.33m, 1600m)]),
                new CustomerInsightCategoryInsights(
                    30,
                    asOfUtc.AddDays(-30),
                    asOfUtc,
                    [new CustomerInsightCategorySpend("housing", "USD", 900m, 56.25m, 1, 850m, 5.88m)],
                    [new CustomerInsightCategorySpend("housing", "USD", 900m, 56.25m, 1, 850m, 5.88m)],
                    [new CustomerInsightCategorySpend("entertainment", "USD", 400m, 25m, 2, 200m, 100m)],
                    [new CustomerInsightConcentrationRatio("USD", 81.25m)],
                    []),
                new CustomerInsightMerchantInsights(
                    30,
                    asOfUtc.AddDays(-30),
                    asOfUtc,
                    [new CustomerInsightMerchantSpend("Landlord", "USD", 900m, 56.25m, 1)],
                    [new CustomerInsightMerchantFrequency("Cinema World", "USD", 2, 400m)],
                    [new CustomerInsightRecurringMerchantCandidate("Netflix", "USD", 20m, 3, 3)],
                    [new CustomerInsightConcentrationRatio("USD", 81.25m)],
                    []),
                new CustomerInsightObligationInsights(
                    30,
                    asOfUtc,
                    asOfUtc.AddDays(30),
                    [new CustomerInsightCommitmentItem("Bill", Guid.NewGuid(), "Electric Co", "USD", 150m, asOfUtc.AddDays(5), "monthly")],
                    [new CustomerInsightCommitmentItem("Subscription", Guid.NewGuid(), "Netflix", "USD", 20m, asOfUtc.AddDays(15), "monthly")],
                    [],
                    [],
                    [],
                    [new CustomerInsightMoneyAmount("USD", 170m)],
                    [new CustomerInsightCoverageRatio("USD", 1800m, 170m, 10.59m)]),
                new CustomerInsightBudgetInsights(
                    1,
                    [new CustomerInsightBudgetSummary(Guid.NewGuid(), asOfUtc.AddDays(-20), "Monthly", 1, "Active")],
                    [new CustomerInsightBudgetCategoryUsage(Guid.NewGuid(), Guid.NewGuid(), "Entertainment", "USD", 300m, 280m, 93.33m, 420m, true)],
                    [],
                    [new CustomerInsightBudgetCategoryUsage(Guid.NewGuid(), Guid.NewGuid(), "Entertainment", "USD", 300m, 280m, 93.33m, 420m, true)]),
                new CustomerInsightGoalInsights(
                    1,
                    [new CustomerInsightGoalProgress(Guid.NewGuid(), "Emergency Fund", "USD", 5000m, 2000m, 40m, asOfUtc.AddMonths(6), 150m, 20)],
                    CustomerInsightSnapshotContract.ConfidenceMedium)),
            [new CustomerInsightSignal(
                "cashflow_stress:USD",
                "risk",
                "Upcoming obligations exceed available cash",
                "Available cash covers less than one lookahead cycle of obligations.",
                CustomerInsightSnapshotContract.SeverityModerate,
                CustomerInsightSnapshotContract.ConfidenceHigh,
                asOfUtc.AddDays(-30),
                asOfUtc,
                ["metrics.obligations.coverageRatios"],
                "Coverage ratio is tightening.")],
            new CustomerInsightRiskOverview(
                CustomerInsightSnapshotContract.SeverityModerate,
                CustomerInsightSnapshotContract.SeverityModerate,
                ["Merchant concentration is high in USD (81.25%)."],
                CustomerInsightSnapshotContract.SeverityModerate,
                ["Category spend is accelerating"]),
            new CustomerInsightEvidence(
                15,
                1,
                [Guid.NewGuid()],
                asOfUtc.AddDays(-180),
                asOfUtc,
                [new CustomerInsightSourceCount("transactions", 15)],
                [new CustomerInsightExcludedDataCount("confirmed_internal_transfers", 1, "Excluded from totals")],
                ["schema:customer_insight_snapshot.v1"],
                []),
            null,
            null);

        dbContext.CustomerInsightSnapshots.Add(new CustomerInsightSnapshot
        {
            Id = snapshotId,
            TenantId = tenantId,
            UserId = userId,
            Status = CustomerInsightSnapshotContract.StatusCurrent,
            AsOfUtc = asOfUtc,
            WindowStartUtc = asOfUtc.AddDays(-CustomerInsightSnapshotContract.BehaviourWindowDays),
            WindowEndUtc = asOfUtc,
            Version = 1,
            SourceHash = "snapshot-hash",
            SnapshotJson = JsonSerializer.Serialize(document),
            GeneratedBy = CustomerInsightSnapshotContract.GeneratorVersion,
            GenerationDurationMs = 120
        });

        dbContext.SaveChanges();
        return snapshotId;
    }

    private static string BuildValidSummaryJson(string headline, string summary)
    {
        var document = new CustomerInsightAiSummaryDocument(
            CustomerInsightAiSummaryContract.SchemaVersion,
            headline,
            summary,
            ["Cash position remains resilient."],
            ["Savings progress is still moving forward."],
            ["Entertainment spend is rising faster than income."],
            ["Review discretionary entertainment categories."],
            ["Ask whether the recent entertainment spike is temporary or recurring."],
            ["metrics.cashPosition.totalBalanceByCurrency", "metrics.categories.categoryTrendDeltas"],
            ["Snapshot includes partial behavioural interpretation and should be cross-checked against live changes."]);

        return JsonSerializer.Serialize(document);
    }
}
