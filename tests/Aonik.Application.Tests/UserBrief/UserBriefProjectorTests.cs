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
        public UserBriefCustomerInsightAiSummaryData? CustomerInsightAiSummary { get; set; }

        public Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserBriefMemoryEntryData>>(MemoryEntries);

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

        public Task<Guid?> GetUserIdForPartyAsync(
            Guid tenantId,
            Guid partyId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Guid.NewGuid());
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
        SpendSummaries: [new UserBriefSpendData("GBP", 800m,
            [new UserBriefCategorySpendData("Food", 400m, 50m), new UserBriefCategorySpendData("Transport", 200m, 25m)],
            new DateTime(2026, 3, 1), DateTime.UtcNow)],
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
            ]
        };
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.Should().NotBeNull();
        brief.User.Name.Should().Be("Ade");
        brief.User.Country.Should().Be("GB");
        brief.Goals.Should().Contain("Track spending").And.Contain("Emergency fund");
        brief.Cash.Should().NotBeNull();
        brief.Cash!.Balance.Should().Be(5000m);
        brief.Cash.Currency.Should().Be("GBP");
        brief.Period.Should().NotBeNull();
        brief.Period!.Inflows.Should().Be(3200m);
        brief.Period.Outflows.Should().Be(2400m);
        brief.Period.Currency.Should().Be("GBP");
        brief.TopCategories.Should().Contain(c => c.Name == "Food" && c.Amount == 400m);
        brief.TopMerchants.Should().Contain(m => m.Name == "Tesco" && m.Amount == 250m);
        brief.Signals.Should().ContainSingle().Which.Title.Should().Be("Late-month spend spike");
        brief.Signals[0].Severity.Should().Be("Moderate");
        brief.Risks.Should().Contain("Budget pressure level: Moderate");
        brief.CashflowRisk.Should().Be(CashflowRisk.Low); // 4500 > 2 * 1200
        brief.AiCanDo.Should().Contain("view_balances");
        brief.AiNeedsApproval.Should().Contain("initiate_payment");
        brief.AsOf.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProjectAsync_Should_FallbackToCategorySpendFromSpendSummaries_When_SnapshotMissing()
    {
        var financeData = new StubFinanceDataProvider
        {
            Data = CreateMinimalFinancialData() with { CustomerInsightSnapshot = null }
        };
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, userContextData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.Period.Should().BeNull();
        brief.TopMerchants.Should().BeEmpty();
        brief.Signals.Should().BeEmpty();
        brief.Risks.Should().BeEmpty();
        brief.TopCategories.Should().Contain(c => c.Name == "Food" && c.Amount == 400m);
        brief.MissingData.Should().Contain("customer_insight_snapshot");
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
    public async Task ProjectAsync_Should_MarkConversationHistoryPresent_When_SummariesExist()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider();
        var userContextData = new StubUserContextDataProvider();
        using var db = CreateDbContext();

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

        brief.MissingData.Should().NotContain("conversation_history");
    }

    [Fact]
    public async Task ProjectAsync_Should_ReturnEmptyCollectionsAndNullCash_When_NoDataExists()
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
                SpendSummaries: [],
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

        brief.Cash.Should().BeNull();
        brief.Period.Should().BeNull();
        brief.TopCategories.Should().BeEmpty();
        brief.TopMerchants.Should().BeEmpty();
        brief.Signals.Should().BeEmpty();
        brief.Risks.Should().BeEmpty();
        brief.User.Country.Should().BeNull();
        brief.CashflowRisk.Should().Be(CashflowRisk.Low);
        brief.MissingData.Should().Contain(["accounts", "transactions", "goals", "bills_and_subscriptions", "customer_insight_snapshot", "conversation_history"]);
    }

    [Fact]
    public async Task ProjectAsync_Should_FallbackToFirstName_When_NoPreferredNameMemory()
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
                SpendSummaries: [new UserBriefSpendData("GBP", 0m, [], new DateTime(2026, 4, 1), new DateTime(2026, 4, 2))],
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

        brief.User.Name.Should().Be("Jaden");
        brief.Goals.Should().Contain("Track spending").And.Contain("Emergency fund");
        brief.MissingData.Should().Contain(["accounts", "transactions", "customer_insight_snapshot"]);
    }
}
