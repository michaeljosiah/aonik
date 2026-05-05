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
using Aonik.Worker.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.PersonalFinance;

public class CustomerInsightAiSummaryJobTests
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
        public Guid? CurrentUserId { get; set; }

        public Guid? GetCurrentUserId() => CurrentUserId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = CurrentUserId ?? Guid.Empty;
            return CurrentUserId.HasValue;
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

    private sealed class StubEnumerator : ICustomerInsightAiSummaryJobSnapshotEnumerator
    {
        private readonly IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget> _snapshots;

        public StubEnumerator(IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget> snapshots)
        {
            _snapshots = snapshots;
        }

        public Task<IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget>> GetNextBatchAsync(
            CustomerInsightAiSummaryJobCheckpoint? checkpoint,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = _snapshots
                .Where(x => checkpoint is null
                    || x.TenantId.CompareTo(checkpoint.Value.TenantId) > 0
                    || (x.TenantId == checkpoint.Value.TenantId && x.UserId.CompareTo(checkpoint.Value.UserId) > 0)
                    || (x.TenantId == checkpoint.Value.TenantId
                        && x.UserId == checkpoint.Value.UserId
                        && x.CustomerInsightSnapshotId.CompareTo(checkpoint.Value.CustomerInsightSnapshotId) > 0))
                .Take(batchSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget>>(filtered);
        }
    }

    private sealed class RecordingSummaryService : ICustomerInsightAiSummaryService
    {
        private readonly Guid _timedOutSnapshotId;
        private readonly List<Guid> _calls = [];

        public RecordingSummaryService(Guid timedOutSnapshotId)
        {
            _timedOutSnapshotId = timedOutSnapshotId;
        }

        public IReadOnlyList<Guid> Calls => _calls;

        public async Task<CustomerInsightAiSummaryResponse> GenerateCurrentSummaryAsync(Guid customerInsightSnapshotId, CancellationToken cancellationToken = default)
        {
            _calls.Add(customerInsightSnapshotId);

            if (customerInsightSnapshotId == _timedOutSnapshotId)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new CustomerInsightAiSummaryResponse(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        customerInsightSnapshotId,
                        Guid.NewGuid(),
                        CustomerInsightAiSummaryContract.StatusFailed,
                        DateTime.UtcNow,
                        CustomerInsightAiSummaryContract.BuildNarrativeVersion("model"),
                        "Customer insight AI summary generation timed out or was cancelled.",
                        null,
                        DateTime.UtcNow,
                        null,
                        null);
                }
            }

            return new CustomerInsightAiSummaryResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                customerInsightSnapshotId,
                Guid.NewGuid(),
                CustomerInsightAiSummaryContract.StatusCurrent,
                DateTime.UtcNow,
                CustomerInsightAiSummaryContract.BuildNarrativeVersion("model"),
                null,
                null,
                DateTime.UtcNow,
                null,
                new CustomerInsightAiSummaryDocument(
                    CustomerInsightAiSummaryContract.SchemaVersion,
                    "Headline",
                    "Summary",
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    []));
        }
    }

    private sealed class QueueChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("QueueChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            const string responseText = """
            {
              "schemaVersion": "customer_insight_ai_summary.v1",
              "headline": "Stable cash position",
              "summary": "Cashflow remains stable with manageable discretionary pressure.",
              "keyObservations": ["Cash buffers remain healthy."],
              "positivePatterns": ["Savings remain on track."],
              "riskPatterns": ["Entertainment spend is rising."],
              "recommendedFocusAreas": ["Review discretionary categories."],
              "conversationSuggestions": ["Ask about month-end spend spikes."],
              "referencedMetrics": ["metrics.cashPosition.totalBalanceByCurrency"],
              "caveats": []
            }
            """;

            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeTaskProfileResolver : IAiTaskProfileResolver
    {
        public Task<AiTaskProfile> ResolveAsync(
            string useCase,
            string? promptName = null,
            string? defaultModelId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AiTaskProfile("model-a", "system", "{{SNAPSHOT_JSON}}"));
    }

    private static FinanceDbContext CreateFinanceDbContext(ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"CustomerInsightAiSummaryJob_Finance_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, tenantProvider);
    }

    private static AiDbContext CreateAiDbContext(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"CustomerInsightAiSummaryJob_Ai_{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, tenantProvider, currentUserProvider, clock);
    }

    [Fact]
    public async Task SnapshotEnumerator_ShouldReturnStableOrderedPagedSnapshots()
    {
        var tenantContext = new TestTenantContext { TenantId = Guid.NewGuid(), ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var currentUserProvider = new StaticCurrentUserProvider();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var tenantA = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var tenantB = Guid.Parse("70000000-0000-0000-0000-000000000002");
        var userA = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var userB = Guid.Parse("71000000-0000-0000-0000-000000000002");
        var userC = Guid.Parse("71000000-0000-0000-0000-000000000003");

        SeedSnapshot(financeDbContext, tenantB, userC, Guid.Parse("72000000-0000-0000-0000-000000000003"));
        SeedSnapshot(financeDbContext, tenantA, userB, Guid.Parse("72000000-0000-0000-0000-000000000002"));
        SeedSnapshot(financeDbContext, tenantA, userA, Guid.Parse("72000000-0000-0000-0000-000000000001"));

        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var enumerator = new CustomerInsightAiSummaryJobSnapshotEnumerator(financeDbContext, summaryReader);

        var firstBatch = await enumerator.GetNextBatchAsync(null, 2);
        var secondBatch = await enumerator.GetNextBatchAsync(
            new CustomerInsightAiSummaryJobCheckpoint(firstBatch[1].TenantId, firstBatch[1].UserId, firstBatch[1].CustomerInsightSnapshotId),
            2);

        firstBatch.Should().Equal(
            new CustomerInsightAiSummaryJobSnapshotTarget(tenantA, userA, Guid.Parse("72000000-0000-0000-0000-000000000001")),
            new CustomerInsightAiSummaryJobSnapshotTarget(tenantA, userB, Guid.Parse("72000000-0000-0000-0000-000000000002")));

        secondBatch.Should().Equal(
            new CustomerInsightAiSummaryJobSnapshotTarget(tenantB, userC, Guid.Parse("72000000-0000-0000-0000-000000000003")));
    }

    [Fact]
    public async Task SnapshotEnumerator_ShouldExcludeSnapshots_WithExistingCurrentOrFailedSummary()
    {
        // Regression test for the runaway OpenAI spend bug discovered on 2026-04-14.
        // Without this exclusion, every cron sweep re-bills OpenAI for every active
        // user's snapshot — even ones already summarised.
        var tenantContext = new TestTenantContext { TenantId = Guid.NewGuid(), ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var currentUserProvider = new StaticCurrentUserProvider();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var tenantId = Guid.Parse("76000000-0000-0000-0000-000000000001");
        var userId = Guid.Parse("76000000-0000-0000-0000-000000000002");
        var summarisedSnapshotId = Guid.Parse("77000000-0000-0000-0000-000000000001");
        var failedSnapshotId = Guid.Parse("77000000-0000-0000-0000-000000000002");
        var pendingSnapshotId = Guid.Parse("77000000-0000-0000-0000-000000000003");
        var supersededSnapshotId = Guid.Parse("77000000-0000-0000-0000-000000000004");

        SeedSnapshot(financeDbContext, tenantId, userId, summarisedSnapshotId);
        SeedSnapshot(financeDbContext, tenantId, userId, failedSnapshotId);
        SeedSnapshot(financeDbContext, tenantId, userId, pendingSnapshotId);
        SeedSnapshot(financeDbContext, tenantId, userId, supersededSnapshotId);

        SeedSummary(aiDbContext, tenantId, userId, summarisedSnapshotId, CustomerInsightAiSummaryContract.StatusCurrent);
        SeedSummary(aiDbContext, tenantId, userId, failedSnapshotId, CustomerInsightAiSummaryContract.StatusFailed);
        SeedSummary(aiDbContext, tenantId, userId, supersededSnapshotId, CustomerInsightAiSummaryContract.StatusSuperseded);

        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var enumerator = new CustomerInsightAiSummaryJobSnapshotEnumerator(financeDbContext, summaryReader);

        var batch = await enumerator.GetNextBatchAsync(null, batchSize: 50);

        // Only the pending snapshot (no summary) and the superseded one (eligible for re-summary)
        // should be returned. Current and Failed are excluded.
        batch.Select(x => x.CustomerInsightSnapshotId).Should().BeEquivalentTo(
            new[] { pendingSnapshotId, supersededSnapshotId });
    }

    private static void SeedSummary(
        AiDbContext aiDbContext,
        Guid tenantId,
        Guid userId,
        Guid snapshotId,
        string status)
    {
        aiDbContext.CustomerInsightAiSummaries.Add(new CustomerInsightAiSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            CustomerInsightSnapshotId = snapshotId,
            AiRunId = Guid.NewGuid(),
            Status = status,
            AsOfUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            NarrativeVersion = CustomerInsightAiSummaryContract.BuildNarrativeVersion("model-a"),
            SummaryJson = "{}"
        });
        aiDbContext.SaveChanges();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueWhenOneSnapshotTimesOut()
    {
        var first = new CustomerInsightAiSummaryJobSnapshotTarget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var second = new CustomerInsightAiSummaryJobSnapshotTarget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var tenantContext = new TestTenantContext();
        var service = new RecordingSummaryService(first.CustomerInsightSnapshotId);
        var job = new CustomerInsightAiSummaryJob(
            new StubEnumerator([first, second]),
            service,
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
            {
                CustomerInsightAiSummary = new CustomerInsightAiSummaryJobOptions
                {
                    BatchSize = 10,
                    SnapshotTimeoutSeconds = 1,
                    SnapshotWarningThresholdSeconds = 1
                }
            }),
            NullLogger<CustomerInsightAiSummaryJob>.Instance);

        var jobDataMap = new JobDataMap();

        await job.ExecuteAsync(jobDataMap, CancellationToken.None);

        service.Calls.Should().ContainInOrder(first.CustomerInsightSnapshotId, second.CustomerInsightSnapshotId);
        tenantContext.TenantId.Should().BeNull();
        CustomerInsightAiSummaryJob.ReadCheckpoint(jobDataMap).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateDuplicateCurrentSummaries_WhenNarrativeVersionIsUnchangedAcrossRuns()
    {
        var tenantContext = new TestTenantContext();
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var currentUserProvider = new StaticCurrentUserProvider();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var financeDbContext = CreateFinanceDbContext(tenantProvider);
        using var aiDbContext = CreateAiDbContext(tenantProvider, currentUserProvider, clock);

        var tenantId = Guid.Parse("73000000-0000-0000-0000-000000000001");
        var userId = Guid.Parse("74000000-0000-0000-0000-000000000001");
        var snapshotId = Guid.Parse("75000000-0000-0000-0000-000000000001");
        SeedSnapshot(financeDbContext, tenantId, userId, snapshotId);

        currentUserProvider.CurrentUserId = userId;

        var snapshotReader = new CustomerInsightSnapshotForAiAdapter(new CustomerInsightSnapshotReader(financeDbContext));
        var summaryReader = new CustomerInsightAiSummaryReader(aiDbContext);
        var summaryService = new CustomerInsightAiSummaryService(
            aiDbContext,
            snapshotReader,
            summaryReader,
            new FakeTaskProfileResolver(),
            new QueueChatClient(),
            new AiRunWriter(aiDbContext, tenantProvider, currentUserProvider, CreateFusionCache()),
            clock,
            NullLogger<CustomerInsightAiSummaryService>.Instance);
        var enumerator = new CustomerInsightAiSummaryJobSnapshotEnumerator(financeDbContext, summaryReader);
        var job = new CustomerInsightAiSummaryJob(
            enumerator,
            summaryService,
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
            {
                CustomerInsightAiSummary = new CustomerInsightAiSummaryJobOptions
                {
                    BatchSize = 1,
                    SnapshotTimeoutSeconds = 30,
                    SnapshotWarningThresholdSeconds = 30
                }
            }),
            NullLogger<CustomerInsightAiSummaryJob>.Instance);

        var jobDataMap = new JobDataMap();

        await job.ExecuteAsync(jobDataMap, CancellationToken.None);
        await job.ExecuteAsync(jobDataMap, CancellationToken.None);

        var summaries = await aiDbContext.CustomerInsightAiSummaries
            .IgnoreQueryFilters()
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        summaries.Should().HaveCount(1);
        summaries[0].Status.Should().Be(CustomerInsightAiSummaryContract.StatusCurrent);
    }

    private static void SeedSnapshot(FinanceDbContext dbContext, Guid tenantId, Guid userId, Guid snapshotId)
    {
        var asOfUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var snapshot = new CustomerInsightSnapshotDocument(
            CustomerInsightSnapshotContract.SchemaVersion,
            userId,
            tenantId,
            asOfUtc,
            new CustomerInsightAnalysisWindow(
                asOfUtc.AddDays(-180),
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
                new CustomerInsightCashPosition(1, [new CustomerInsightMoneyAmount("USD", 2000m)], [new CustomerInsightMoneyAmount("USD", 2000m)], [new CustomerInsightAccountBalance(Guid.NewGuid(), "Main", "Bank", "USD", 2000m, 100m)], [new CustomerInsightConcentrationRatio("USD", 100m)]),
                new CustomerInsightIncomeSummary(30, asOfUtc.AddDays(-30), asOfUtc, [new CustomerInsightMoneyAmount("USD", 3000m)], [new CustomerInsightMoneyAmount("USD", 3000m)], "monthly", [new CustomerInsightSourceAmount("Employer", "USD", 3000m, 1)], [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main", "USD", 3000m, 1)], [new CustomerInsightPeriodDelta("USD", 3000m, 2900m, 100m, 3.45m)]),
                new CustomerInsightExpenseSummary(30, asOfUtc.AddDays(-30), asOfUtc, [new CustomerInsightMoneyAmount("USD", 1500m)], [new CustomerInsightMoneyAmount("USD", 1000m)], [new CustomerInsightMoneyAmount("USD", 500m)], [new CustomerInsightMoneyAmount("USD", 1200m)], [new CustomerInsightMoneyAmount("USD", 300m)], [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main", "USD", 1500m, 3)], [new CustomerInsightPeriodDelta("USD", 1500m, 1400m, 100m, 7.14m)], [new CustomerInsightAverageSpend("USD", 350m, 1500m)]),
                new CustomerInsightCategoryInsights(30, asOfUtc.AddDays(-30), asOfUtc, [new CustomerInsightCategorySpend("food", "USD", 500m, 33.33m, 2, 450m, 11.11m)], [new CustomerInsightCategorySpend("food", "USD", 500m, 33.33m, 2, 450m, 11.11m)], [new CustomerInsightCategorySpend("food", "USD", 500m, 33.33m, 2, 450m, 11.11m)], [new CustomerInsightConcentrationRatio("USD", 33.33m)], []),
                new CustomerInsightMerchantInsights(30, asOfUtc.AddDays(-30), asOfUtc, [new CustomerInsightMerchantSpend("Tesco", "USD", 500m, 33.33m, 2)], [new CustomerInsightMerchantFrequency("Tesco", "USD", 2, 500m)], [], [new CustomerInsightConcentrationRatio("USD", 33.33m)], []),
                new CustomerInsightObligationInsights(30, asOfUtc, asOfUtc.AddDays(30), [], [], [], [], [], [], []),
                new CustomerInsightBudgetInsights(0, [], [], [], []),
                new CustomerInsightGoalInsights(0, [], CustomerInsightSnapshotContract.ConfidenceLow)),
            [],
            new CustomerInsightRiskOverview(CustomerInsightSnapshotContract.SeverityLow, CustomerInsightSnapshotContract.SeverityLow, [], CustomerInsightSnapshotContract.SeverityLow, []),
            new CustomerInsightEvidence(0, 0, [], asOfUtc.AddDays(-180), asOfUtc, [], [], [], []),
            null,
            null);

        dbContext.CustomerInsightSnapshots.Add(new CustomerInsightSnapshot
        {
            Id = snapshotId,
            TenantId = tenantId,
            UserId = userId,
            Status = CustomerInsightSnapshotContract.StatusCurrent,
            AsOfUtc = asOfUtc,
            WindowStartUtc = asOfUtc.AddDays(-180),
            WindowEndUtc = asOfUtc,
            Version = 1,
            SourceHash = $"hash-{snapshotId}",
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot),
            GeneratedBy = CustomerInsightSnapshotContract.GeneratorVersion,
            GenerationDurationMs = 10
        });

        dbContext.SaveChanges();
    }

    /// <summary>
    /// In-memory FusionCache for AiRunWriter's kill-switch cache.
    /// </summary>
    private static IFusionCache CreateFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFusionCache>();
    }
}
