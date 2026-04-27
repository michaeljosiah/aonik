using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Insights;
using Aonik.Finance.Entities.Ledger;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Insights;

/// <summary>
/// Focused coverage of the Wave 4b extension to MySpaceSummaryService:
/// AgentOpsToday, CashPositionUpdatedAt, CashTimeline (currency + 30-day
/// historical series), and AgentProposals — wiring through to the
/// SharedKernel cross-module services.
/// </summary>
public class MySpaceSummaryServiceTests
{
    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }

    private sealed class StubAiRunStats(int count) : IAiRunStatsService
    {
        public Task<int> CountForTodayAsync(CancellationToken ct = default) => Task.FromResult(count);
    }

    private sealed class StubAgentProposalQuery(IReadOnlyList<AgentProposalSummary> proposals)
        : IAgentProposalQueryService
    {
        public Task<IReadOnlyList<AgentProposalSummary>> ListPendingAsync(
            int take = 5,
            CancellationToken ct = default) =>
            Task.FromResult(proposals);
    }

    private sealed class StubTenantCurrencyProvider(List<string> codes) : ITenantCurrencyProvider
    {
        public Task<List<string>> GetTenantCurrencyCodesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(codes);
    }

    private sealed class StubPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static MySpaceSummaryService CreateService(
        FinanceDbContext db,
        Guid tenantId,
        int aiRunsToday = 0,
        IReadOnlyList<AgentProposalSummary>? proposals = null,
        List<string>? currencyCodes = null) =>
        new(
            db,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new StubPermissionService(),
            new StubAiRunStats(aiRunsToday),
            new StubAgentProposalQuery(proposals ?? Array.Empty<AgentProposalSummary>()),
            new StubTenantCurrencyProvider(currencyCodes ?? new List<string> { "NGN", "USD", "GBP" }));

    [Fact]
    public async Task GetSummaryAsync_Should_PopulateExtendedFields_When_AllSourcesReturnData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);

        var asset = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = Guid.NewGuid(),
            AccountType = "Asset",
            Name = "Cash",
            Code = "1000",
            DimensionsJson = "{}",
        };
        db.LedgerAccounts.Add(asset);

        var entryToday = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = asset.LedgerId,
            Timestamp = DateTime.UtcNow,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            Status = "Posted",
        };
        var entryYesterday = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = asset.LedgerId,
            Timestamp = DateTime.UtcNow.Date.AddDays(-1).AddHours(10),
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            Status = "Posted",
        };
        db.JournalEntries.AddRange(entryToday, entryYesterday);

        db.JournalEntryLines.AddRange(
            new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = entryToday.Id,
                LedgerAccountId = asset.Id,
                Direction = "Debit",
                Amount = 500m,
                Currency = "NGN",
                DimensionsJson = "{}",
            },
            new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = entryYesterday.Id,
                LedgerAccountId = asset.Id,
                Direction = "Debit",
                Amount = 200m,
                Currency = "NGN",
                DimensionsJson = "{}",
            });
        await db.SaveChangesAsync();

        var proposal = new AgentProposalSummary(
            Id: Guid.NewGuid(),
            AgentName: "Billing",
            AgentDomain: "Finance",
            AgentIconUrl: null,
            Confidence: 0.94m,
            Summary: "Match bank txn to INV-2041",
            Reason: null,
            RiskTier: "Low",
            CreatedAt: DateTime.UtcNow);

        var service = CreateService(
            db, tenantId,
            aiRunsToday: 47,
            proposals: new[] { proposal },
            currencyCodes: new List<string> { "NGN", "USD" });

        var result = await service.GetSummaryAsync();

        result.AgentOpsToday.Should().Be(47);
        result.CashPositionUpdatedAt.Should().BeCloseTo(entryToday.Timestamp, TimeSpan.FromSeconds(1));

        result.CashTimeline.Currency.Should().Be("NGN");
        result.CashTimeline.Historical.Should().HaveCount(30);
        result.CashTimeline.Historical[^1].Balance.Should().Be(700m,
            "the running balance includes both today's debit (500) and yesterday's debit (200)");
        result.CashTimeline.Projected.Should().BeEmpty(
            "Wave 4b ships historical only — projection lands in Wave 4c");
        result.CashTimeline.Events.Should().BeEmpty();
        result.CashTimeline.ProjectedLow.Should().BeNull();

        result.AgentProposals.Should().HaveCount(1);
        result.AgentProposals[0].AgentName.Should().Be("Billing");
        result.AgentProposals[0].Confidence.Should().Be(0.94m);
    }

    [Fact]
    public async Task GetSummaryAsync_Should_FallBackToUsd_When_TenantHasNoCurrencies()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var service = CreateService(db, tenantId, currencyCodes: new List<string>());

        var result = await service.GetSummaryAsync();

        result.CashTimeline.Currency.Should().Be("USD");
        result.CashTimeline.Historical.Should().HaveCount(30,
            "we always emit a 30-point series, padded with zero balances when there are no entries");
        result.CashTimeline.Historical.All(p => p.Balance == 0m).Should().BeTrue();
    }

    [Fact]
    public async Task GetSummaryAsync_Should_ReturnNullCashUpdatedAt_When_TenantHasNoEntries()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var service = CreateService(db, tenantId);

        var result = await service.GetSummaryAsync();

        result.CashPositionUpdatedAt.Should().BeNull();
        result.AgentOpsToday.Should().Be(0);
        result.AgentProposals.Should().BeEmpty();
    }
}
