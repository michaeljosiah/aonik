using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Tests.Ledger;

/// <summary>
/// The double-entry ledger invariant: every posted journal entry MUST
/// have its debit total equal to its credit total, both totals must
/// be positive, and the entry must contain at least two lines (one
/// debit, one credit).
///
/// This is the most important rule in <c>LedgerService.AddJournalEntryAsync</c>
/// — without it the ledger is no longer the "source of financial
/// truth" called out in CLAUDE.md. Locking it down with explicit
/// regression tests so a future refactor can't quietly relax it.
/// </summary>
public class LedgerInvariantTests
{
    [Fact]
    public async Task PostJournalEntry_WithBalancedDebitsAndCredits_Succeeds()
    {
        // Arrange — one ledger, one Asset + one Income account.
        var fixture = await TestFixture.CreateAsync();
        var (debit, credit) = await fixture.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            fixture.LedgerId,
            "REF-001",
            "Revenue recognition",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", 250m, "USD", "Cash in"),
                new(credit.Id, "Credit", 250m, "USD", "Revenue"),
            });

        // Act
        var result = await fixture.Service.AddJournalEntryAsync(request);

        // Assert — service returned a posted entry and the lines
        // round-trip through the DbContext with their amounts intact.
        result.Lines.Should().HaveCount(2);
        var sumDebits = result.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount);
        var sumCredits = result.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount);
        sumDebits.Should().Be(sumCredits);
        sumDebits.Should().Be(250m);
    }

    [Theory]
    [InlineData(100, 99)]     // credits short
    [InlineData(99, 100)]     // debits short
    [InlineData(0, 100)]      // zero debits
    [InlineData(100, 0)]      // zero credits
    public async Task PostJournalEntry_WithUnbalancedAmounts_Throws(decimal debitAmount, decimal creditAmount)
    {
        var fixture = await TestFixture.CreateAsync();
        var (debit, credit) = await fixture.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            fixture.LedgerId,
            "REF-BAD",
            "Should not post",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", debitAmount, "USD", null),
                new(credit.Id, "Credit", creditAmount, "USD", null),
            });

        // Act + Assert
        var act = () => fixture.Service.AddJournalEntryAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*debits and credits must balance*");

        // And nothing was persisted — invariant violation must NOT leave
        // a half-written entry behind.
        var persisted = await fixture.DbContext.JournalEntries.AnyAsync();
        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task PostJournalEntry_WithFewerThanTwoLines_Throws()
    {
        var fixture = await TestFixture.CreateAsync();
        var (debit, _) = await fixture.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            fixture.LedgerId,
            "REF-SINGLE",
            "Should not post",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", 100m, "USD", null),
            });

        var act = () => fixture.Service.AddJournalEntryAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least two lines*");
    }

    [Fact]
    public async Task PostJournalEntry_WithMultipleLines_BalancingAcrossThemSucceeds()
    {
        // The invariant is sum(debits) == sum(credits), not pairwise.
        // A 100-debit can be balanced by two 50-credits and the entry
        // is still valid.
        var fixture = await TestFixture.CreateAsync();
        var (cash, _) = await fixture.SeedAssetAndIncomeAccountsAsync("USD");
        var expense = await fixture.Service.CreateAccountAsync(
            new CreateLedgerAccountRequest(fixture.LedgerId, "Office Supplies", "6100", "Expense"));
        var prepaid = await fixture.Service.CreateAccountAsync(
            new CreateLedgerAccountRequest(fixture.LedgerId, "Prepaid Rent", "1200", "Asset"));

        var request = new AddJournalEntryRequest(
            fixture.LedgerId,
            "REF-SPLIT",
            "Cash paying two expense lines",
            new List<AddJournalEntryLineRequest>
            {
                new(cash.Id, "Credit", 100m, "USD", null),
                new(expense.Id, "Debit", 70m, "USD", null),
                new(prepaid.Id, "Debit", 30m, "USD", null),
            });

        var result = await fixture.Service.AddJournalEntryAsync(request);

        result.Lines.Should().HaveCount(3);
        result.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount).Should().Be(100m);
        result.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount).Should().Be(100m);
    }

    // ─── Local fixture ──────────────────────────────────────────────────
    //
    // Self-contained, no shared state across tests — each fixture spins
    // up a fresh InMemory FinanceDbContext keyed by a random Guid. The
    // existing LedgerServiceTests in Aonik.Application.Tests follow the
    // same pattern; we duplicate it here rather than depending on that
    // assembly so Aonik.Finance.Tests can stand alone.

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;
        public TestCurrentUserProvider(Guid userId) => _userId = userId;
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _userId; return true; }
    }

    private sealed class TestFixture
    {
        public required FinanceDbContext DbContext { get; init; }
        public required LedgerService Service { get; init; }
        public required Guid LedgerId { get; init; }

        public static async Task<TestFixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"LedgerInvariantTests_{Guid.NewGuid()}")
                .Options;
            var dbContext = new FinanceDbContext(options, new TestTenantProvider(tenantId));

            var service = new LedgerService(
                dbContext,
                new TestTenantProvider(tenantId),
                new AllowAllPermissionService(),
                new TestCurrentUserProvider(Guid.NewGuid()),
                new FinanceMetrics());

            var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
            return new TestFixture { DbContext = dbContext, Service = service, LedgerId = ledger.Id };
        }

        public async Task<(LedgerAccountResponse Asset, LedgerAccountResponse Income)> SeedAssetAndIncomeAccountsAsync(string currency)
        {
            var asset = await Service.CreateAccountAsync(new CreateLedgerAccountRequest(LedgerId, "Cash", "1000", "Asset"));
            var income = await Service.CreateAccountAsync(new CreateLedgerAccountRequest(LedgerId, "Revenue", "4000", "Income"));
            return (asset, income);
        }
    }
}
