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
        result.CashTimeline.Projected.Should().HaveCount(30,
            "Wave 4c.4 emits a 30-day naive projection forward from the last historical point");
        result.CashTimeline.ProjectedLow.Should().NotBeNull(
            "ProjectedLow tracks the minimum across the last historical point and every projected point");

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
        result.CashTimeline.AvailableCurrencies.Should().Contain("NGN",
            "the test fixture seeds NGN/USD/GBP into the tenant currency stub");
    }

    [Fact]
    public async Task GetSummaryAsync_Should_FilterCashTimelineByRequestedCurrency()
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

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = asset.LedgerId,
            Timestamp = DateTime.UtcNow,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            Status = "Posted",
        };
        db.JournalEntries.Add(entry);

        // 1000 NGN debit + 50 USD debit on the same entry — but we want only
        // the requested currency to count toward each timeline.
        db.JournalEntryLines.AddRange(
            new JournalEntryLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = entry.Id,
                LedgerAccountId = asset.Id, Direction = "Debit", Amount = 1000m,
                Currency = "NGN", DimensionsJson = "{}",
            },
            new JournalEntryLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = entry.Id,
                LedgerAccountId = asset.Id, Direction = "Debit", Amount = 50m,
                Currency = "USD", DimensionsJson = "{}",
            });
        await db.SaveChangesAsync();

        var service = CreateService(
            db, tenantId,
            currencyCodes: new List<string> { "NGN", "USD", "GBP" });

        var ngnResult = await service.GetSummaryAsync(currencyOverride: "NGN");
        ngnResult.CashTimeline.Currency.Should().Be("NGN");
        ngnResult.CashTimeline.Historical[^1].Balance.Should().Be(1000m);

        var usdResult = await service.GetSummaryAsync(currencyOverride: "USD");
        usdResult.CashTimeline.Currency.Should().Be("USD");
        usdResult.CashTimeline.Historical[^1].Balance.Should().Be(50m);
    }

    [Fact]
    public async Task GetSummaryAsync_Should_FallBackToPrimary_When_OverrideNotInTenantSet()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var service = CreateService(
            db, tenantId,
            currencyCodes: new List<string> { "NGN", "USD" });

        // EUR is not in the tenant's configured currency list.
        var result = await service.GetSummaryAsync(currencyOverride: "EUR");

        result.CashTimeline.Currency.Should().Be("NGN", "primary is used when override is unrecognised");
        result.CashTimeline.AvailableCurrencies.Should().Equal("NGN", "USD");
    }

    [Fact]
    public async Task GetSummaryAsync_Should_PopulateRevenueEventsFromInvoiceDueDates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);

        var todayUtc = DateTime.UtcNow.Date;
        db.Invoices.AddRange(
            new Aonik.Finance.Entities.Billing.Invoice
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                CustomerAccountId = Guid.NewGuid(),
                IssueDate = todayUtc.AddDays(-5),
                DueDate = todayUtc.AddDays(7),
                Currency = "NGN", Subtotal = 5000m, TaxTotal = 0m, DiscountTotal = 0m, Total = 5000m,
                Status = "Issued", ProvenanceJson = "{}",
            },
            new Aonik.Finance.Entities.Billing.Invoice
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                CustomerAccountId = Guid.NewGuid(),
                IssueDate = todayUtc.AddDays(-3),
                DueDate = todayUtc.AddDays(14),
                Currency = "NGN", Subtotal = 3000m, TaxTotal = 0m, DiscountTotal = 0m, Total = 3000m,
                Status = "Issued", ProvenanceJson = "{}",
            },
            // Outside the window — should be filtered out
            new Aonik.Finance.Entities.Billing.Invoice
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                CustomerAccountId = Guid.NewGuid(),
                IssueDate = todayUtc.AddDays(-2),
                DueDate = todayUtc.AddDays(45),
                Currency = "NGN", Subtotal = 1000m, TaxTotal = 0m, DiscountTotal = 0m, Total = 1000m,
                Status = "Issued", ProvenanceJson = "{}",
            },
            // Different currency — should be filtered out for an NGN timeline
            new Aonik.Finance.Entities.Billing.Invoice
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                CustomerAccountId = Guid.NewGuid(),
                IssueDate = todayUtc.AddDays(-1),
                DueDate = todayUtc.AddDays(10),
                Currency = "USD", Subtotal = 200m, TaxTotal = 0m, DiscountTotal = 0m, Total = 200m,
                Status = "Issued", ProvenanceJson = "{}",
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, currencyCodes: new List<string> { "NGN" });
        var result = await service.GetSummaryAsync();

        result.CashTimeline.Events.Should().HaveCount(2,
            "only the two NGN invoices due inside the 30-day window are surfaced");
        result.CashTimeline.Events.Should().OnlyContain(e => e.Kind == "revenue");
        result.CashTimeline.Events.Should().OnlyContain(e => e.Date <= todayUtc.AddDays(30));
    }

    [Fact]
    public async Task BuildProjection_Should_ExtrapolateForwardFromHistoricalTrend()
    {
        // Arrange a 30-day historical series with a steady +100/day climb
        // over the trailing window so the moving-average delta is +100.
        var today = DateTime.UtcNow.Date;
        var historical = Enumerable.Range(0, 30)
            .Select(i => new Aonik.Finance.Contracts.Models.Insights.CashTimelinePointDto(
                today.AddDays(-29 + i), 1000m + 100m * i))
            .ToList();

        // Use reflection to invoke the private static helper — keeps the
        // test lightweight without exposing internals.
        var method = typeof(MySpaceSummaryService)
            .GetMethod("BuildProjection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var result = method.Invoke(null, new object[] { historical, 30 })!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        var projected = (IReadOnlyList<Aonik.Finance.Contracts.Models.Insights.CashTimelinePointDto>)tuple[0]!;
        var projectedLow = (decimal?)tuple[1];
        var projectedLowAt = (DateTime?)tuple[2];

        projected.Should().HaveCount(30);
        projected[0].Date.Should().Be(today.AddDays(1),
            "projection starts the day after the last historical point");
        projected[0].Balance.Should().Be(historical[^1].Balance + 100m,
            "first projected point applies one day of average delta");
        projected[^1].Balance.Should().Be(historical[^1].Balance + 100m * 30,
            "last projected point reflects 30 days of compounded average delta");
        projectedLow.Should().Be(historical[^1].Balance,
            "an upward trend means the lowest forward balance is today's balance");
        projectedLowAt.Should().Be(historical[^1].Date);
    }
}
