using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
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

        public Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserBriefMemoryEntryData>>(MemoryEntries);

        public Task<IReadOnlyList<UserBriefInsightData>> GetBehaviouralInsightsAsync(
            Guid tenantId, Guid userId, int maxResults = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserBriefInsightData>>(Insights.Take(maxResults).ToList());
    }

    private static AgentsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"UserBriefTest_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider());
    }

    private static UserBriefFinancialData CreateMinimalFinancialData() => new(
        TotalBalance: 5000m,
        AvailableBalance: 4500m,
        PrimaryCurrency: "GBP",
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
            Insights = [new("UserBehaviour", "Late-month spending spike", "You tend to spend 30% more.", 0.82m, null)]
        };
        using var db = CreateDbContext();
        var logger = NullLogger<UserBriefProjector>.Instance;

        var projector = new UserBriefProjector(financeData, aiData, db, logger);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.Should().NotBeNull();
        brief.UserProfile.PreferredName.Should().Be("Ade");
        brief.UserProfile.CommunicationStyle.Should().Be("concise");
        brief.UserProfile.CorridorCountries.Should().Contain("GB").And.Contain("NG");
        brief.UserProfile.HouseholdContext.Should().Be("Supporting mother + sister in Lagos");
        brief.CurrentState.CashSummary.TotalBalance.Should().Be(5000m);
        brief.CurrentState.NextBills.Should().HaveCount(1);
        brief.CurrentState.NextBills[0].Payee.Should().Be("Rent");
        brief.CurrentState.Subscriptions.Should().HaveCount(1);
        brief.FinancialFocus.CurrentGoals.Should().HaveCount(1);
        brief.FinancialFocus.SupportObligations.Should().HaveCount(1);
        brief.BehaviouralInsights.Should().HaveCount(1);
        brief.CashflowRisk.Should().Be(CashflowRisk.Low); // 4500 > 2 * 1200
        brief.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
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
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, db, NullLogger<UserBriefProjector>.Instance);
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
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CashflowRisk.Should().Be(CashflowRisk.Moderate);
    }

    [Fact]
    public async Task ProjectAsync_Should_IncludeConversationMemory_When_SummariesExist()
    {
        var financeData = new StubFinanceDataProvider();
        var aiData = new StubAiDataProvider();
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

        var projector = new UserBriefProjector(financeData, aiData, db, NullLogger<UserBriefProjector>.Instance);
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
                0m, 0m, "GBP", [], [], null, [], [], [], [], null)
        };
        var aiData = new StubAiDataProvider();
        using var db = CreateDbContext();

        var projector = new UserBriefProjector(financeData, aiData, db, NullLogger<UserBriefProjector>.Instance);
        var brief = await projector.ProjectAsync(TenantId, UserId);

        brief.CurrentState.NextBills.Should().BeEmpty();
        brief.CurrentState.Subscriptions.Should().BeEmpty();
        brief.FinancialFocus.CurrentGoals.Should().BeEmpty();
        brief.BehaviouralInsights.Should().BeEmpty();
        brief.RecentConversationMemory.Should().BeEmpty();
        brief.CashflowRisk.Should().Be(CashflowRisk.Low); // No obligations = Low risk
    }
}
