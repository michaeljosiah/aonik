using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalFinanceNarrativeInsightsServiceTests
{
    [Fact]
    public async Task GenerateSpendingNarrativeAsync_ShouldUseCurrentAiSummary_WhenAvailable()
    {
        // Arrange
        var runWriter = new FakeAiRunWriter();
        var insightWriter = new FakeInsightWriter();
        var snapshot = CreateSnapshotResponse();
        var summary = new CustomerInsightAiSummaryResponse(
            Guid.NewGuid(),
            snapshot.UserId,
            snapshot.Id,
            Guid.NewGuid(),
            CustomerInsightAiSummaryContract.StatusCurrent,
            snapshot.AsOfUtc,
            CustomerInsightAiSummaryContract.BuildNarrativeVersion("model-a"),
            null,
            null,
            DateTime.UtcNow,
            null,
            new CustomerInsightAiSummaryDocument(
                CustomerInsightAiSummaryContract.SchemaVersion,
                "Stable cash position",
                "Cashflow remains stable but discretionary spend is rising.",
                ["Cash buffers are healthy."],
                ["Savings are progressing."],
                ["Entertainment spend is trending upward."],
                ["Review discretionary categories before month end."],
                ["Ask whether the current spike is temporary."],
                ["metrics.cashPosition.totalBalanceByCurrency"],
                []));

        var service = new PersonalFinanceNarrativeInsightsService(
            new FakeSnapshotReader(snapshot),
            new FakeSnapshotService(snapshot),
            new FakeSummaryReader(summary),
            insightWriter,
            runWriter,
            new StaticCurrentUserProvider(snapshot.UserId));

        var request = new GeneratePersonalSpendingNarrativeRequest(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null);

        // Act
        var response = await service.GenerateSpendingNarrativeAsync(request);

        // Assert
        response.AiRunId.Should().Be(summary.AiRunId);
        response.SubjectType.Should().Be("CustomerInsightSnapshot");
        response.SubjectId.Should().Be(snapshot.Id);
        response.Title.Should().Be("Stable cash position");
        response.Summary.Should().Contain("Cashflow remains stable");
        runWriter.StartedRuns.Should().BeEmpty();
        runWriter.CompletedRuns.Should().BeEmpty();
        insightWriter.SaveCount.Should().Be(1);
        insightWriter.LastUserId.Should().Be(snapshot.UserId);
        insightWriter.LastMetadataJson.Should().Contain(snapshot.Id.ToString());
    }

    [Fact]
    public async Task GenerateSpendingNarrativeAsync_ShouldUseDeterministicSnapshotFallback_WhenAiSummaryIsMissing()
    {
        // Arrange
        var runWriter = new FakeAiRunWriter();
        var insightWriter = new FakeInsightWriter();
        var snapshot = CreateSnapshotResponse();

        var service = new PersonalFinanceNarrativeInsightsService(
            new FakeSnapshotReader(snapshot),
            new FakeSnapshotService(snapshot),
            new FakeSummaryReader(null),
            insightWriter,
            runWriter,
            new StaticCurrentUserProvider(snapshot.UserId));

        var request = new GeneratePersonalSpendingNarrativeRequest(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null);

        // Act
        var response = await service.GenerateSpendingNarrativeAsync(request);

        // Assert
        response.AiRunId.Should().Be(runWriter.StartedRuns[0]);
        response.SubjectType.Should().Be("CustomerInsightSnapshot");
        response.SubjectId.Should().Be(snapshot.Id);
        response.Summary.Should().Contain("Top spend category is food");
        runWriter.StartedRuns.Should().HaveCount(1);
        runWriter.CompletedRuns.Should().HaveCount(1);
        runWriter.CompletedRuns[0].RunId.Should().Be(runWriter.StartedRuns[0]);
        runWriter.FailedRuns.Should().BeEmpty();
        insightWriter.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task GenerateSpendingNarrativeAsync_ShouldGenerateSnapshot_WhenCurrentSnapshotIsMissing()
    {
        // Arrange
        var generatedSnapshot = CreateSnapshotResponse();
        var snapshotService = new FakeSnapshotService(generatedSnapshot);
        var service = new PersonalFinanceNarrativeInsightsService(
            new FakeSnapshotReader(null),
            snapshotService,
            new FakeSummaryReader(null),
            new FakeInsightWriter(),
            new FakeAiRunWriter(),
            new StaticCurrentUserProvider(generatedSnapshot.UserId));

        var request = new GeneratePersonalSpendingNarrativeRequest(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null);

        // Act
        await service.GenerateSpendingNarrativeAsync(request);

        // Assert
        snapshotService.GenerateCalls.Should().ContainSingle(x => x == generatedSnapshot.UserId);
    }

    private static CustomerInsightSnapshotResponse CreateSnapshotResponse()
    {
        var userId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var asOfUtc = DateTime.UtcNow;

        return new CustomerInsightSnapshotResponse(
            snapshotId,
            userId,
            CustomerInsightSnapshotContract.StatusCurrent,
            asOfUtc,
            asOfUtc.AddDays(-180),
            asOfUtc,
            1,
            "hash",
            CustomerInsightSnapshotContract.GeneratorVersion,
            10,
            null,
            null,
            DateTime.UtcNow,
            null,
            new CustomerInsightSnapshotDocument(
                CustomerInsightSnapshotContract.SchemaVersion,
                userId,
                Guid.NewGuid(),
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
                    new CustomerInsightCashPosition(
                        1,
                        [new CustomerInsightMoneyAmount("USD", 2500m)],
                        [new CustomerInsightMoneyAmount("USD", 2500m)],
                        [new CustomerInsightAccountBalance(Guid.NewGuid(), "Main", "Bank", "USD", 2500m, 100m)],
                        [new CustomerInsightConcentrationRatio("USD", 100m)]),
                    new CustomerInsightIncomeSummary(
                        30,
                        asOfUtc.AddDays(-30),
                        asOfUtc,
                        [new CustomerInsightMoneyAmount("USD", 3200m)],
                        [new CustomerInsightMoneyAmount("USD", 3200m)],
                        "monthly",
                        [new CustomerInsightSourceAmount("Employer", "USD", 3200m, 1)],
                        [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main", "USD", 3200m, 1)],
                        [new CustomerInsightPeriodDelta("USD", 3200m, 3100m, 100m, 3.23m)]),
                    new CustomerInsightExpenseSummary(
                        30,
                        asOfUtc.AddDays(-30),
                        asOfUtc,
                        [new CustomerInsightMoneyAmount("USD", 1800m)],
                        [new CustomerInsightMoneyAmount("USD", 1200m)],
                        [new CustomerInsightMoneyAmount("USD", 600m)],
                        [new CustomerInsightMoneyAmount("USD", 1400m)],
                        [new CustomerInsightMoneyAmount("USD", 400m)],
                        [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main", "USD", 1800m, 4)],
                        [new CustomerInsightPeriodDelta("USD", 1800m, 1500m, 300m, 20m)],
                        [new CustomerInsightAverageSpend("USD", 420m, 1800m)]),
                    new CustomerInsightCategoryInsights(
                        30,
                        asOfUtc.AddDays(-30),
                        asOfUtc,
                        [new CustomerInsightCategorySpend("food", "USD", 700m, 38.89m, 3, 500m, 40m)],
                        [new CustomerInsightCategorySpend("food", "USD", 700m, 38.89m, 3, 500m, 40m)],
                        [new CustomerInsightCategorySpend("food", "USD", 700m, 38.89m, 3, 500m, 40m)],
                        [new CustomerInsightConcentrationRatio("USD", 38.89m)],
                        []),
                    new CustomerInsightMerchantInsights(
                        30,
                        asOfUtc.AddDays(-30),
                        asOfUtc,
                        [new CustomerInsightMerchantSpend("Tesco", "USD", 500m, 27.78m, 2)],
                        [new CustomerInsightMerchantFrequency("Tesco", "USD", 2, 500m)],
                        [],
                        [new CustomerInsightConcentrationRatio("USD", 27.78m)],
                        []),
                    new CustomerInsightObligationInsights(
                        30,
                        asOfUtc,
                        asOfUtc.AddDays(30),
                        [new CustomerInsightCommitmentItem("Bill", Guid.NewGuid(), "Rent", "USD", 1200m, asOfUtc.AddDays(5), "monthly")],
                        [],
                        [],
                        [new CustomerInsightMoneyAmount("USD", 1200m)],
                        [new CustomerInsightCoverageRatio("USD", 2500m, 1200m, 2.08m)]),
                    new CustomerInsightBudgetInsights(0, [], [], [], []),
                    new CustomerInsightGoalInsights(0, [], CustomerInsightSnapshotContract.ConfidenceLow)),
                [new CustomerInsightSignal(
                    "late_month_spike",
                    "spending",
                    "Late-month spending spike",
                    "Spending rises near month end.",
                    CustomerInsightSnapshotContract.SeverityModerate,
                    CustomerInsightSnapshotContract.ConfidenceHigh,
                    asOfUtc.AddDays(-30),
                    asOfUtc,
                    ["metrics.categories.topCategoriesByAmount"],
                    "Observed multiple end-of-month spikes.")],
                new CustomerInsightRiskOverview(
                    CustomerInsightSnapshotContract.SeverityModerate,
                    CustomerInsightSnapshotContract.SeverityLow,
                    [],
                    CustomerInsightSnapshotContract.SeverityLow,
                    []),
                new CustomerInsightEvidence(
                    10,
                    0,
                    [Guid.NewGuid()],
                    asOfUtc.AddDays(-180),
                    asOfUtc,
                    [],
                    [],
                    [],
                    []),
                null,
                null));
    }

    private sealed class FakeSnapshotReader : ICustomerInsightSnapshotReader
    {
        private readonly CustomerInsightSnapshotResponse? _snapshot;

        public FakeSnapshotReader(CustomerInsightSnapshotResponse? snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<CustomerInsightSnapshotResponse?> GetCurrentSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public Task<CustomerInsightSnapshotResponse?> GetSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot?.Id == snapshotId ? _snapshot : null);

        public Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> GetSnapshotHistoryAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>>([]);
    }

    private sealed class FakeSnapshotService : ICustomerInsightSnapshotService
    {
        private readonly CustomerInsightSnapshotResponse _snapshot;

        public FakeSnapshotService(CustomerInsightSnapshotResponse snapshot)
        {
            _snapshot = snapshot;
        }

        public List<Guid> GenerateCalls { get; } = [];

        public Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            GenerateCalls.Add(userId);
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class FakeSummaryReader : ICustomerInsightAiSummaryReader
    {
        private readonly CustomerInsightAiSummaryResponse? _summary;

        public FakeSummaryReader(CustomerInsightAiSummaryResponse? summary)
        {
            _summary = summary;
        }

        public Task<CustomerInsightAiSummaryResponse?> GetCurrentSummaryForSnapshotAsync(Guid customerInsightSnapshotId, CancellationToken cancellationToken = default)
            => Task.FromResult(_summary?.CustomerInsightSnapshotId == customerInsightSnapshotId ? _summary : null);

        public Task<CustomerInsightAiSummaryResponse?> GetSummaryAsync(Guid summaryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_summary?.Id == summaryId ? _summary : null);
    }

    private sealed class FakeInsightWriter : IInsightWriter
    {
        public int SaveCount { get; private set; }
        public Guid? LastUserId { get; private set; }
        public string? LastMetadataJson { get; private set; }

        public Task<InsightResponse> SaveInsightAsync(
            string subjectType,
            Guid subjectId,
            string title,
            string summary,
            string? metadataJson = null,
            Guid? userId = null,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastUserId = userId;
            LastMetadataJson = metadataJson;

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

        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            StartedRuns.Add(id);
            return Task.FromResult(id);
        }

        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
        {
            CompletedRuns.Add((aiRunId, outputRef));
            return Task.CompletedTask;
        }

        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
        {
            FailedRuns.Add((aiRunId, failureReason));
            return Task.CompletedTask;
        }

        public async Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default)
        {
            var id = await StartRunAsync(useCase, inputRefsJson, cancellationToken);
            if (!string.Equals(outcome, "Started", StringComparison.OrdinalIgnoreCase))
            {
                await MarkRunCompletedAsync(id, null, cancellationToken);
            }

            return id;
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
}
