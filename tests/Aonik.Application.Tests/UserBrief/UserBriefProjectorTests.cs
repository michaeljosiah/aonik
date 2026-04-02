using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.UserBrief;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.UserBrief;

public class UserBriefProjectorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private class StubFinanceDataProvider : IUserBriefDataProvider
    {
        public UserBriefFinancialData Data { get; set; } = CreateMinimalFinancialData();

        public Task<UserBriefFinancialData> GetFinancialDataAsync(
            Guid tenantId, Guid userId, UserBriefFinancialDataRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Data);
    }

    private class StubAiDataProvider : IUserBriefAiDataProvider
    {
        public List<UserBriefMemoryEntryData> MemoryEntries { get; set; } = [];
        public List<UserBriefInsightData> Insights { get; set; } = [];
        public UserBriefCustomerInsightAiSummaryData? CustomerInsightAiSummary { get; set; }

        public Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserBriefMemoryEntryData>>(MemoryEntries);

        public Task<IReadOnlyList<UserBriefInsightData>> GetBehaviouralInsightsAsync(
            Guid tenantId, Guid userId, int maxResults = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserBriefInsightData>>(Insights.Take(maxResults).ToList());

        public Task<UserBriefCustomerInsightAiSummaryData?> GetCurrentCustomerInsightAiSummaryAsync(
            Guid tenantId,
            Guid userId,
            Guid customerInsightSnapshotId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CustomerInsightAiSummary);
    }

    private class StubUserContextDataProvider : IUserBriefContextDataProvider
    {
        public UserBriefContextData Data { get; set; } = new(
            FullName: "Jaden Josiah",
            FirstName: "Jaden",
            LastName: "Josiah",
            Email: "jaden@example.com",
            PhoneNumber: "+447700900123",
            UserCreatedAt: new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
            SetupProfile: new UserBriefSetupProfileData(
                SelectedUseCases: ["Track spending", "Build savings"],
                AccountSourceTypes: ["Manual"],
                ConnectChoice: "later",
                Responsibilities: ["Myself"],
                SupportType: null,
                FinancialGoals: ["Emergency fund"],
                Completed: true));

        public Task<UserBriefContextData> GetUserContextDataAsync(
            Guid tenantId,
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Data);
    }

    private static AgentsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"UserBriefTest_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider());
    }

    private static UserBriefFinancialData CreateMinimalFinancialData() => new(
        AccountCount: 2,
        TransactionCount: 12,
        TotalBalance: 5000m,
        AvailableBalance: 4500m,
        PrimaryCurrency: "GBP",
        CustomerInsightSnapshot: CreateSnapshotData(isPartial: false),
        UpcomingBills: [new UserBriefBillData(Guid.NewGuid(), "Rent", 1200m, "GBP", DateTime.UtcNow.AddDays(5), true)],
        ActiveSubscriptions: [new UserBriefSubscriptionData(Guid.NewGuid(), "Netflix", 15.99m, "GBP", DateTime.UtcNow.AddDays(10))],
        SpendSummary: new UserBriefSpendData(800m,
            [new UserBriefCategorySpendData("Food", 400m, 50m), new UserBriefCategorySpendData("Transport", 200m, 25m)],
            new DateTime(2026, 3, 1), DateTime.UtcNow),
        BudgetPressure: [new UserBriefBudgetPressureData("Food", 500m, 450m, 90m)],
        ActiveGoals: [new UserBriefGoalData(Guid.NewGuid(), "Emergency Fund", 5000m, 2000m, "GBP", DateTime.UtcNow.AddMonths(6), "active")],
        SupportObligations: [new UserBriefObligationData("Mother - Lagos", 200m, "GBP", "monthly", DateTime.UtcNow.AddDays(3))],
        CorridorCountries: ["GB", "NG"],
        HouseholdContext: "Supporting mother + sister in Lagos");

    private static UserBriefCustomerInsightSnapshotData CreateSnapshotData(bool isPartial) => new(
        SnapshotId: Guid.NewGuid(),
        AsOfUtc: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
        WindowStartUtc: new DateTime(2025, 10, 4, 0, 0, 0, DateTimeKind.Utc),
        WindowEndUtc: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
        IsPartial: isPartial,
        CoverageWarnings: isPartial ? ["Goals domain unavailable"] : [],
        TotalBalanceByCurrency: [new UserBriefSnapshotMoneyData("GBP", 5000m)],
        TotalInflowsByCurrency: [new UserBriefSnapshotMoneyData("GBP", 3200m)],
        TotalOutflowsByCurrency: [new UserBriefSnapshotMoneyData("GBP", 2400m)],
        TopCategorySpend: [new UserBriefSnapshotSpendData("Food", "GBP", 400m, 50m), new UserBriefSnapshotSpendData("Transport", "GBP", 200m, 25m)],
        TopMerchantSpend: [new UserBriefSnapshotSpendData("Tesco", "GBP", 250m, 31.25m)],
        UpcomingObligationsByCurrency: [new UserBriefSnapshotMoneyData("GBP", 1200m)],
        ObligationCoverageSummaries: ["GBP: coverage ratio 3.75"],
        BudgetPressureCategories: ["Food at 90% of budget"],
        GoalProgressHighlights: ["Emergency Fund: 40% complete, about 12 months to target"],
        KeyBehaviourSignals:
        [
            new UserBriefSnapshotSignalData(
                "late_month_spike",
                "spending",
                "Late-month spend spike",
                "Spending rises near month end.",
                "Moderate",
                "High")
        ],
        RiskFlags: ["Budget pressure level: Moderate", "Merchant concentration is high in GBP (45%)."]);

    private static UserBriefCustomerInsightAiSummaryData CreateAiSummaryData() => new(
        Headline: "Cash position is stable with rising discretionary pressure",
        Summary: "Core cashflow remains healthy, but entertainment and food spending need tighter follow-up.",
        KeyObservations:
        [
            "Income still covers current obligations comfortably.",
            "Late-month spending spikes keep showing up.",
            "Food and entertainment are the main pressure points."
        ],
        RecommendedFocusAreas:
        [
            "Review discretionary categories before month end.",
            "Confirm whether the latest spending spike was exceptional."
        ],
        ReferencedMetricKeys:
        [
            "metrics.cashPosition.totalBalanceByCurrency",
            "metrics.categories.topCategoriesByAmount",
            "signals.late_month_spike"
        ],
        Caveats: ["Interpretation is grounded in the latest deterministic snapshot."]);

    [Fact]
    public async Task ProjectAsync_Should_AssembleCompleteBrief()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider
        {
            MemoryEntries =
            [
                new("Preference", "communication.style", "\"concise\"", 1.0m, "UserStated"),
                new("Identity", "identity.preferred_name", "\"Ade\"", 1.0m, "UserStated")
            ],
            Insights = [new("UserBehaviour", "Late-month spending spike", "You tend to spend 30% more.", 0.82m, null)],
            CustomerInsightAiSummary = CreateAiSummaryData()
        };
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();
        var logger = NullLogger<UserBriefProjector>.Instance;

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, logger);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.Should().NotBeNull();
        brief.UserProfile.PreferredName.Should().Be("Ade");
        brief.UserProfile.FullName.Should().Be("Jaden Josiah");
        brief.UserProfile.GivenName.Should().Be("Jaden");
        brief.UserProfile.Email.Should().Be("jaden@example.com");
        brief.UserProfile.CommunicationStyle.Should().Be("concise");
        brief.UserProfile.CorridorCountries.Should().Contain("GB").And.Contain("NG");
        brief.UserProfile.HouseholdContext.Should().Be("Supporting mother + sister in Lagos");
        brief.SetupProfile.Should().NotBeNull();
        brief.SetupProfile!.SelectedUseCases.Should().Contain("Track spending");
        brief.DataAvailability.IsNewUser.Should().BeFalse();
        brief.CurrentState.CashSummary.TotalBalance.Should().Be(5000m);
        brief.CurrentState.NextBills.Should().HaveCount(1);
        brief.CurrentState.NextBills[0].Payee.Should().Be("Rent");
        brief.CurrentState.Subscriptions.Should().HaveCount(1);
        brief.FinancialFocus.CurrentGoals.Should().HaveCount(1);
        brief.FinancialFocus.SupportObligations.Should().HaveCount(1);
        brief.CustomerInsightSnapshot.Should().NotBeNull();
        brief.CustomerInsightSnapshot!.IsPartial.Should().BeFalse();
        brief.CustomerInsightSnapshot.TopCategorySpend.Should().ContainSingle(x => x.Name == "Food");
        brief.CustomerInsightAiInterpretation.Should().NotBeNull();
        brief.CustomerInsightAiInterpretation!.Headline.Should().Contain("Cash position is stable");
        brief.BehaviouralInsights.Should().HaveCount(1);
        brief.CashflowRisk.Should().Be(CashflowRisk.Low); // 4500 > 2 * 1200
        brief.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProjectAsync_Should_FallbackToPartialSnapshot_When_SnapshotIsPartial()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = CreateMinimalFinancialData() with
            {
                CustomerInsightSnapshot = CreateSnapshotData(isPartial: true)
            }
        };
        var aiData = new StubAiDataProvider
        {
            CustomerInsightAiSummary = CreateAiSummaryData()
        };
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CustomerInsightSnapshot.Should().NotBeNull();
        brief.CustomerInsightSnapshot!.IsPartial.Should().BeTrue();
        brief.CustomerInsightSnapshot.CoverageWarnings.Should().Contain("Deterministic customer insight snapshot is partial.");
        brief.CustomerInsightAiInterpretation.Should().NotBeNull();
        brief.CustomerInsightAiInterpretation!.Caveats.Should().Contain("Underlying deterministic snapshot is partial; treat AI interpretation as lower certainty.");
    }

    [Fact]
    public async Task ProjectAsync_Should_OmitAiInterpretation_When_NoCurrentAiSummaryExists()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CustomerInsightSnapshot.Should().NotBeNull();
        brief.CustomerInsightAiInterpretation.Should().BeNull();
    }

    [Fact]
    public async Task ProjectAsync_Should_PrioritizeSnapshotAndAiInterpretation_When_TokenBudgetIsTight()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider
        {
            CustomerInsightAiSummary = CreateAiSummaryData(),
            Insights =
            [
                new("UserBehaviour", "Insight 1", "Summary 1", 0.9m, null),
                new("UserBehaviour", "Insight 2", "Summary 2", 0.9m, null),
                new("UserBehaviour", "Insight 3", "Summary 3", 0.9m, null),
                new("UserBehaviour", "Insight 4", "Summary 4", 0.9m, null)
            ]
        };
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        db.ChatThreads.Add(new ChatThread
        {
            TenantId = TenantId,
            UserId = UserId,
            Title = "History",
            Status = ChatThreadStatus.Archived,
            LastMessageAt = DateTime.UtcNow,
            MessageCount = 2
        });
        await db.SaveChangesAsync();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId, new UserBriefOptions { TokenBudget = 200 });

        brief.CustomerInsightSnapshot.Should().NotBeNull();
        brief.CustomerInsightAiInterpretation.Should().NotBeNull();
        brief.BehaviouralInsights.Count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ProjectAsync_Should_DeriveCashflowRisk_High_When_BalanceLessThanObligations()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = CreateMinimalFinancialData() with
            {
                TotalBalance = 500m,
                AvailableBalance = 500m,
                UpcomingBills = [new UserBriefBillData(Guid.NewGuid(), "Rent", 1200m, "GBP", DateTime.UtcNow.AddDays(5), true)]
            }
        };
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CashflowRisk.Should().Be(CashflowRisk.High);
    }

    [Fact]
    public async Task ProjectAsync_Should_DeriveCashflowRisk_Moderate_When_BalanceBetween1xAnd2x()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = CreateMinimalFinancialData() with
            {
                TotalBalance = 1800m,
                AvailableBalance = 1800m,
                UpcomingBills = [new UserBriefBillData(Guid.NewGuid(), "Rent", 1200m, "GBP", DateTime.UtcNow.AddDays(5), true)]
            }
        };
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CashflowRisk.Should().Be(CashflowRisk.Moderate);
    }

    [Fact]
    public async Task ProjectAsync_Should_IncludeConversationMemory_When_SummariesExist()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        // Seed a conversation summary
        var thread = new ChatThread
        {
            TenantId = TenantId,
            UserId = UserId,
            Title = "Test Thread",
            Status = ChatThreadStatus.Archived,
            LastMessageAt = DateTime.UtcNow.AddHours(-2),
            MessageCount = 5
        };
        db.ChatThreads.Add(thread);

        db.ConversationSummaries.Add(new ConversationSummary
        {
            TenantId = TenantId,
            UserId = UserId,
            ChatThreadId = thread.Id,
            SessionStartedAt = DateTime.UtcNow.AddHours(-3),
            SessionEndedAt = DateTime.UtcNow.AddHours(-2),
            SummaryText = "User asked about budget overruns and decided to reduce food spending.",
            KeyDecisionsJson = """[{"decision": "Reduce food budget by 20%", "context": "Overspent last 2 months"}]""",
            OpenLoopsJson = """[{"description": "Set up auto-transfer to savings", "priority": "medium"}]""",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.RecentConversationMemory.Should().HaveCount(1);
        brief.RecentConversationMemory[0].Summary.Should().Contain("budget overruns");
        brief.RecentConversationMemory[0].OpenLoops.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProjectAsync_Should_ReturnEmptyCollections_When_NoDataExists()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = new UserBriefFinancialData(
                AccountCount: 0,
                TransactionCount: 0,
                TotalBalance: 0m,
                AvailableBalance: 0m,
                PrimaryCurrency: "GBP",
                CustomerInsightSnapshot: null,
                UpcomingBills: [],
                ActiveSubscriptions: [],
                SpendSummary: null,
                BudgetPressure: [],
                ActiveGoals: [],
                SupportObligations: [],
                CorridorCountries: [],
                HouseholdContext: null)
        };
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CurrentState.NextBills.Should().BeEmpty();
        brief.CurrentState.Subscriptions.Should().BeEmpty();
        brief.FinancialFocus.CurrentGoals.Should().BeEmpty();
        brief.BehaviouralInsights.Should().BeEmpty();
        brief.RecentConversationMemory.Should().BeEmpty();
        brief.CashflowRisk.Should().Be(CashflowRisk.Low); // No obligations = Low risk
    }

    [Fact]
    public async Task ProjectAsync_Should_FallbackToUserNameAndMarkNewUser_When_NoPreferredNameOrFinancialHistoryExists()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = new UserBriefFinancialData(
                AccountCount: 0,
                TransactionCount: 0,
                TotalBalance: 0m,
                AvailableBalance: 0m,
                PrimaryCurrency: "GBP",
                CustomerInsightSnapshot: null,
                UpcomingBills: [],
                ActiveSubscriptions: [],
                SpendSummary: new UserBriefSpendData(0m, [], new DateTime(2026, 4, 1), new DateTime(2026, 4, 2)),
                BudgetPressure: [],
                ActiveGoals: [],
                SupportObligations: [],
                CorridorCountries: [],
                HouseholdContext: null)
        };
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider
        {
            Data = new UserBriefContextData(
                FullName: "Jaden Josiah",
                FirstName: "Jaden",
                LastName: "Josiah",
                Email: "jaden@example.com",
                PhoneNumber: null,
                UserCreatedAt: new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                SetupProfile: new UserBriefSetupProfileData(
                    SelectedUseCases: ["Track spending"],
                    AccountSourceTypes: ["Manual"],
                    ConnectChoice: "later",
                    Responsibilities: ["Myself"],
                    SupportType: null,
                    FinancialGoals: ["Emergency fund"],
                    Completed: false))
        };
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.UserProfile.PreferredName.Should().Be("Jaden");
        brief.UserProfile.FullName.Should().Be("Jaden Josiah");
        brief.SetupProfile.Should().NotBeNull();
        brief.DataAvailability.IsNewUser.Should().BeTrue();
        brief.DataAvailability.HasLimitedFinancialData.Should().BeTrue();
        brief.DataAvailability.MissingDataAreas.Should().Contain(["accounts", "transactions", "customer_insight_snapshot"]);
        brief.DataAvailability.Summary.Should().Contain("new Payabo user");
    }
}
