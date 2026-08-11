using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

// The Ledger TYPE and the Aonik.Finance.Entities.Ledger NAMESPACE share a name; the codebase
// aliases it in LedgerService, FinanceTenantProvisioningContributor and AonikDbContext too.
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Application.Tests.Finance;

/// <summary>
/// Spec 088 P1 acceptance: a module outside Finance can post a balanced, dimensioned entry to a
/// named ledger; the ledger is tenant-checked; codes resolve within that ledger; and an ambiguous
/// tenant makes the resolver throw rather than choose.
/// </summary>
public class JournalWriterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private static readonly Guid UserId = Guid.NewGuid();
        public Guid? GetCurrentUserId() => UserId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = UserId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    }

    private static FinanceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(),
            new TestCurrentUserProvider(),
            new TestClock());

    private static async Task<(Guid LedgerId, FinanceDbContext Db)> SeedLedgerAsync(
        params (string Code, string Type)[] accounts)
    {
        var db = CreateContext();
        var ledgerId = Guid.NewGuid();

        db.Ledgers.Add(new LedgerEntity { Id = ledgerId, TenantId = TenantId, BaseCurrency = "GBP", IsCanonical = true });

        foreach (var (code, type) in accounts)
        {
            db.LedgerAccounts.Add(new LedgerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                LedgerId = ledgerId,
                Code = code,
                Name = code,
                AccountType = type,
                DimensionsJson = "{}"
            });
        }

        await db.SaveChangesAsync();
        return (ledgerId, db);
    }

    private static JournalWriter Writer(FinanceDbContext db) => new(db, new TestTenantProvider(), new TestClock());

    private static PostJournalCommand PrepaidCreditPurchase(Guid ledgerId, Guid sourceId, decimal amount = 3.99m) =>
        new(
            ledgerId,
            "EntitlementPurchaseSettlement",
            sourceId,
            [
                new JournalLineSpec("1000", JournalDirections.Debit, amount, "GBP", "Cash received"),
                new JournalLineSpec("2210", JournalDirections.Credit, amount, "GBP", "Deferred entitlement revenue",
                    """{"meterCode":"animated-videos"}""")
            ]);

    [Fact]
    public async Task PostAsync_Should_WriteABalancedDimensionedEntry_ToTheNamedLedger()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));

        var result = await Writer(db).PostAsync(PrepaidCreditPurchase(ledgerId, Guid.NewGuid()));

        result.AlreadyExisted.Should().BeFalse();

        var entry = await db.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == result.JournalEntryId);
        entry.LedgerId.Should().Be(ledgerId);
        entry.Lines.Should().HaveCount(2);

        // The dimension is what makes per-meter margin derivable from the ledger alone.
        entry.Lines.Single(l => l.Direction == JournalDirections.Credit)
            .DimensionsJson.Should().Contain("animated-videos");
    }

    [Fact]
    public async Task PostAsync_Should_BeIdempotent_On_SourceTypeAndSourceId()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));
        var sourceId = Guid.NewGuid();
        var writer = Writer(db);

        var first = await writer.PostAsync(PrepaidCreditPurchase(ledgerId, sourceId));
        var second = await writer.PostAsync(PrepaidCreditPurchase(ledgerId, sourceId));

        // At-least-once delivery must not double-post financial truth.
        second.JournalEntryId.Should().Be(first.JournalEntryId);
        second.AlreadyExisted.Should().BeTrue();
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PostAsync_Should_Reject_When_TheEntryDoesNotBalance()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));

        var act = async () => await Writer(db).PostAsync(new PostJournalCommand(
            ledgerId, "Test", Guid.NewGuid(),
            [
                new JournalLineSpec("1000", JournalDirections.Debit, 10m, "GBP"),
                new JournalLineSpec("2210", JournalDirections.Credit, 9m, "GBP")
            ]));

        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*does not balance*");
    }

    [Fact]
    public async Task PostAsync_Should_Reject_When_LinesMixCurrencies()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));

        var act = async () => await Writer(db).PostAsync(new PostJournalCommand(
            ledgerId, "Test", Guid.NewGuid(),
            [
                new JournalLineSpec("1000", JournalDirections.Debit, 10m, "GBP"),
                new JournalLineSpec("2210", JournalDirections.Credit, 10m, "USD")
            ]));

        // Balancing across unlike units is meaningless, not merely unusual.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*single-currency*");
    }

    [Fact]
    public async Task PostAsync_Should_Reject_AnAccountCodeFromAnotherLedger()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));

        // A second ledger in the same tenant, with a code the first does not have.
        var otherLedgerId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = otherLedgerId, TenantId = TenantId, BaseCurrency = "USD" });
        db.LedgerAccounts.Add(new LedgerAccount
        {
            Id = Guid.NewGuid(), TenantId = TenantId, LedgerId = otherLedgerId,
            Code = "9999", Name = "Elsewhere", AccountType = "Asset", DimensionsJson = "{}"
        });
        await db.SaveChangesAsync();

        var act = async () => await Writer(db).PostAsync(new PostJournalCommand(
            ledgerId, "Test", Guid.NewGuid(),
            [
                new JournalLineSpec("1000", JournalDirections.Debit, 5m, "GBP"),
                new JournalLineSpec("9999", JournalDirections.Credit, 5m, "GBP")
            ]));

        // Codes are unique per ledger — resolving tenant-wide is the mistake LedgerId prevents.
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*9999*");
    }

    [Fact]
    public async Task PostAsync_Should_Reject_ALedgerBelongingToAnotherTenant()
    {
        var (_, db) = await SeedLedgerAsync(("1000", "Asset"));

        var foreignLedgerId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = foreignLedgerId, TenantId = Guid.NewGuid(), BaseCurrency = "GBP" });
        await db.SaveChangesAsync();

        // Balanced on purpose: structural validation runs before the ledger lookup, so an
        // unbalanced entry would fail on arithmetic and never exercise the tenant check.
        var act = async () => await Writer(db).PostAsync(new PostJournalCommand(
            foreignLedgerId, "Test", Guid.NewGuid(),
            [
                new JournalLineSpec("1000", JournalDirections.Debit, 1m, "GBP"),
                new JournalLineSpec("1000", JournalDirections.Credit, 1m, "GBP")
            ]));

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*was not found in this tenant*");
    }

    [Fact]
    public async Task PostAsync_Should_Reject_TheReservedManualSourceType()
    {
        var (ledgerId, db) = await SeedLedgerAsync(("1000", "Asset"), ("2210", "Liability"));

        var act = async () => await Writer(db).PostAsync(new PostJournalCommand(
            ledgerId, JournalDirections.ManualSourceType, Guid.Empty,
            [
                new JournalLineSpec("1000", JournalDirections.Debit, 1m, "GBP"),
                new JournalLineSpec("2210", JournalDirections.Credit, 1m, "GBP")
            ]));

        // "Manual" is excluded from the idempotency index, so accepting it would silently drop
        // the guarantee the contract advertises.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*reserved*");
    }

    [Fact]
    public async Task LedgerResolver_Should_ResolveTheSoleLedger_EvenWhenUnmarked()
    {
        var db = CreateContext();
        var ledgerId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = ledgerId, TenantId = TenantId, BaseCurrency = "GBP", IsCanonical = false });
        await db.SaveChangesAsync();

        var resolved = await new LedgerResolver(db, new TestTenantProvider()).GetCanonicalLedgerIdAsync();

        resolved.Should().Be(ledgerId);
    }

    [Fact]
    public async Task LedgerResolver_Should_Throw_When_SeveralLedgersAndNoneCanonical()
    {
        var db = CreateContext();
        db.Ledgers.Add(new LedgerEntity { Id = Guid.NewGuid(), TenantId = TenantId, BaseCurrency = "GBP" });
        db.Ledgers.Add(new LedgerEntity { Id = Guid.NewGuid(), TenantId = TenantId, BaseCurrency = "USD" });
        await db.SaveChangesAsync();

        var act = async () => await new LedgerResolver(db, new TestTenantProvider()).GetCanonicalLedgerIdAsync();

        // Refusing beats guessing: the two ledgers here have different base currencies, so an
        // arbitrary pick would post in the wrong one.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*none is marked canonical*");
    }

    [Fact]
    public async Task LedgerResolver_Should_PreferTheMarkedLedger_When_SeveralExist()
    {
        var db = CreateContext();
        var canonicalId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = Guid.NewGuid(), TenantId = TenantId, BaseCurrency = "USD" });
        db.Ledgers.Add(new LedgerEntity { Id = canonicalId, TenantId = TenantId, BaseCurrency = "GBP", IsCanonical = true });
        await db.SaveChangesAsync();

        var resolved = await new LedgerResolver(db, new TestTenantProvider()).GetCanonicalLedgerIdAsync();

        resolved.Should().Be(canonicalId);
    }
}
