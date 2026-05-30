using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.IntegrationTests.Support;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.IntegrationTests.Ledger;

/// <summary>
/// The double-entry ledger invariant — sum(debits) == sum(credits), both totals
/// positive, at least two lines — exercised against a real SQL Server
/// (Testcontainers) rather than the EF Core InMemory provider.
///
/// Running on real SQL Server is what makes the "nothing was persisted on an
/// invariant violation" assertion meaningful: InMemory has no transactions, so a
/// rolled-back write there proves nothing about production. Here the rollback is
/// a genuine SQL Server transaction abort, and the entity mappings (decimal
/// precision, tenant query filter) are the production ones.
///
/// Mirrors the InMemory <c>LedgerInvariantTests</c> in Aonik.Finance.Tests. One
/// container is shared via <see cref="SqlServerCollection"/>, which serialises
/// the tests; each test resets the database first. Skips (not fails) when Docker
/// is unavailable.
/// </summary>
[Collection(SqlServerContainerFixture.CollectionName)]
public class LedgerInvariantSqlServerTests
{
    private readonly SqlServerContainerFixture _fixture;

    public LedgerInvariantSqlServerTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task PostJournalEntry_WithBalancedDebitsAndCredits_Succeeds()
    {
        var ledger = await CreateLedgerFixtureAsync();
        var (debit, credit) = await ledger.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            ledger.LedgerId,
            "REF-001",
            "Revenue recognition",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", 250m, "USD", "Cash in"),
                new(credit.Id, "Credit", 250m, "USD", "Revenue"),
            });

        var result = await ledger.Service.AddJournalEntryAsync(request);

        result.Lines.Should().HaveCount(2);
        var sumDebits = result.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount);
        var sumCredits = result.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount);
        sumDebits.Should().Be(sumCredits);
        sumDebits.Should().Be(250m);
    }

    [SkippableTheory]
    [InlineData(100, 99)]     // credits short
    [InlineData(99, 100)]     // debits short
    [InlineData(0, 100)]      // zero debits
    [InlineData(100, 0)]      // zero credits
    public async Task PostJournalEntry_WithUnbalancedAmounts_Throws(decimal debitAmount, decimal creditAmount)
    {
        var ledger = await CreateLedgerFixtureAsync();
        var (debit, credit) = await ledger.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            ledger.LedgerId,
            "REF-BAD",
            "Should not post",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", debitAmount, "USD", null),
                new(credit.Id, "Credit", creditAmount, "USD", null),
            });

        var act = () => ledger.Service.AddJournalEntryAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*debits and credits must balance*");

        // And nothing was persisted — on real SQL Server the failed post must
        // roll back its transaction, leaving no half-written entry behind.
        var persisted = await ledger.DbContext.JournalEntries.AnyAsync();
        persisted.Should().BeFalse();
    }

    [SkippableFact]
    public async Task PostJournalEntry_WithFewerThanTwoLines_Throws()
    {
        var ledger = await CreateLedgerFixtureAsync();
        var (debit, _) = await ledger.SeedAssetAndIncomeAccountsAsync("USD");

        var request = new AddJournalEntryRequest(
            ledger.LedgerId,
            "REF-SINGLE",
            "Should not post",
            new List<AddJournalEntryLineRequest>
            {
                new(debit.Id, "Debit", 100m, "USD", null),
            });

        var act = () => ledger.Service.AddJournalEntryAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least two lines*");
    }

    [SkippableFact]
    public async Task PostJournalEntry_WithMultipleLines_BalancingAcrossThemSucceeds()
    {
        // The invariant is sum(debits) == sum(credits), not pairwise. A 100-debit
        // can be balanced by two credits and the entry is still valid.
        var ledger = await CreateLedgerFixtureAsync();
        var (cash, _) = await ledger.SeedAssetAndIncomeAccountsAsync("USD");
        var expense = await ledger.Service.CreateAccountAsync(
            new CreateLedgerAccountRequest(ledger.LedgerId, "Office Supplies", "6100", "Expense"));
        var prepaid = await ledger.Service.CreateAccountAsync(
            new CreateLedgerAccountRequest(ledger.LedgerId, "Prepaid Rent", "1200", "Asset"));

        var request = new AddJournalEntryRequest(
            ledger.LedgerId,
            "REF-SPLIT",
            "Cash paying two expense lines",
            new List<AddJournalEntryLineRequest>
            {
                new(cash.Id, "Credit", 100m, "USD", null),
                new(expense.Id, "Debit", 70m, "USD", null),
                new(prepaid.Id, "Debit", 30m, "USD", null),
            });

        var result = await ledger.Service.AddJournalEntryAsync(request);

        result.Lines.Should().HaveCount(3);
        result.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount).Should().Be(100m);
        result.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount).Should().Be(100m);
    }

    /// <summary>
    /// Resets the shared database, then builds a FinanceDbContext + LedgerService
    /// over the container connection and seeds a starter ledger — the SQL Server
    /// analogue of the InMemory TestFixture in Aonik.Finance.Tests. Skips the test
    /// up front when Docker (and therefore the container) is unavailable.
    /// </summary>
    private async Task<LedgerTestFixture> CreateLedgerFixtureAsync()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason ?? "SQL Server container unavailable.");

        await _fixture.ResetAsync();

        var tenantProvider = new TestTenantProvider();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;
        var dbContext = new FinanceDbContext(options, tenantProvider);

        var service = new LedgerService(
            dbContext,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(),
            new FinanceMetrics());

        var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
        return new LedgerTestFixture(dbContext, service, ledger.Id);
    }

    private sealed class LedgerTestFixture
    {
        public FinanceDbContext DbContext { get; }
        public LedgerService Service { get; }
        public Guid LedgerId { get; }

        public LedgerTestFixture(FinanceDbContext dbContext, LedgerService service, Guid ledgerId)
        {
            DbContext = dbContext;
            Service = service;
            LedgerId = ledgerId;
        }

        public async Task<(LedgerAccountResponse Asset, LedgerAccountResponse Income)> SeedAssetAndIncomeAccountsAsync(string currency)
        {
            var asset = await Service.CreateAccountAsync(new CreateLedgerAccountRequest(LedgerId, "Cash", "1000", "Asset"));
            var income = await Service.CreateAccountAsync(new CreateLedgerAccountRequest(LedgerId, "Revenue", "4000", "Income"));
            return (asset, income);
        }
    }
}
