using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using LedgerAccountEntity = Aonik.Finance.Entities.Ledger.LedgerAccount;

namespace Aonik.Application.Tests.Finance;

/// <summary>
/// Spec 088 P5 acceptance: an unregistered order type still credits 4000 as one line; a registered
/// one routes and splits.
///
/// The first half matters more than the second. This changes a method every invoice on the
/// platform flows through, so the tests that prove nothing moved for bill payments, remittances
/// and product purchases are the ones carrying the risk.
/// </summary>
public class SettlementRoutingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    /// <summary>Stands in for the resolver Spec 087 will register.</summary>
    private sealed class EntitlementPurchaseResolver : ISettlementRevenueResolver
    {
        public IReadOnlyCollection<string> OrderTypes => ["EntitlementPurchase"];

        public SettlementCredit Resolve(string orderType, SettlementLineContext line)
            => new(
                AccountCode: "2210",
                AccountName: "Deferred Revenue - Purchased Entitlements",
                AccountType: "Liability",
                DimensionsJson: $$"""{"meterCode":"{{line.Description}}"}""");
    }

    private static FinanceDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider());

    private static async Task<Guid> SeedLedgerAsync(FinanceDbContext db)
    {
        var ledgerId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = ledgerId, TenantId = TenantId, BaseCurrency = "GBP" });

        foreach (var (code, name, type) in new[]
                 {
                     ("1000", "Cash", "Asset"),
                     ("2100", "Payments Clearing", "Liability"),
                     ("4000", "Operating Revenue", "Revenue")
                 })
        {
            db.LedgerAccounts.Add(new LedgerAccountEntity
            {
                Id = Guid.NewGuid(), TenantId = TenantId, LedgerId = ledgerId,
                Code = code, Name = name, AccountType = type, DimensionsJson = "{}"
            });
        }

        await db.SaveChangesAsync();
        return ledgerId;
    }

    private static async Task<Invoice> SeedInvoiceAsync(
        FinanceDbContext db,
        string? orderType,
        params (string Description, decimal Amount)[] lines)
    {
        Guid? orderId = null;

        if (orderType is not null)
        {
            orderId = Guid.NewGuid();
            db.Orders.Add(new Order
            {
                Id = orderId.Value,
                TenantId = TenantId,
                OrderType = orderType,
                CurrencyIn = "GBP",
                AmountIn = lines.Sum(l => l.Amount),
                Status = "Complete",
                ProvenanceJson = "{}"
            });
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            OrderId = orderId,
            CustomerAccountId = Guid.NewGuid(),
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Currency = "GBP",
            Status = "Issued",
            ProvenanceJson = "{}",
            Total = lines.Sum(l => l.Amount)
        };
        db.Invoices.Add(invoice);

        foreach (var (description, amount) in lines)
        {
            db.InvoiceLines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceId = invoice.Id,
                Description = description,
                Quantity = 1,
                UnitPrice = amount,
                LineTotal = amount,
                MetadataJson = "{}"
            });
        }

        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task<List<(string Code, string Direction, decimal Amount, string Dimensions)>> ReadEntryAsync(
        FinanceDbContext db, Guid invoiceId)
    {
        var entry = await db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceType == "InvoiceSettlement" && e.SourceId == invoiceId);

        var accounts = await db.LedgerAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Code);

        return entry.Lines
            .Select(l => (accounts[l.LedgerAccountId], l.Direction, l.Amount, l.DimensionsJson))
            .ToList();
    }

    // ---- regression: nothing moves for everything that already existed --------------------

    [Theory]
    [InlineData("BillPayment")]
    [InlineData("Remittance")]
    [InlineData("ProductPurchase")]
    [InlineData("PurchaseOrder")]
    public async Task UnregisteredOrderTypes_Should_StillCredit4000_AsOneLine(string orderType)
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, orderType, ("Service", 50m), ("Fee", 10m));

        var poster = new LedgerPostingService(db, null, [new EntitlementPurchaseResolver()]);
        await poster.PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);

        // Even with a resolver present and a multi-line invoice, an unclaimed type takes the
        // untouched path: two lines, the whole total, credited to 4000.
        lines.Should().HaveCount(2);
        lines.Should().ContainSingle(l => l.Direction == "Debit" && l.Code == "2100" && l.Amount == 60m);
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "4000" && l.Amount == 60m);
    }

    [Fact]
    public async Task AnInvoiceWithNoOrder_Should_StillCredit4000_AsOneLine()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, orderType: null, ("Standalone service", 40m));

        var poster = new LedgerPostingService(db, null, [new EntitlementPurchaseResolver()]);
        await poster.PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "4000" && l.Amount == 40m);
    }

    [Fact]
    public async Task WithNoResolversRegistered_Should_BehaveExactlyAsBefore()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, "EntitlementPurchase", ("videos", 3.99m));

        // The shape every existing fixture constructs — no resolvers at all.
        var poster = new LedgerPostingService(db);
        await poster.PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);
        lines.Should().HaveCount(2);
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "4000");
    }

    // ---- the new capability ---------------------------------------------------------------

    [Fact]
    public async Task ARegisteredOrderType_Should_RouteAndSplitPerLine()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, "EntitlementPurchase",
            ("animated-videos", 3.99m), ("stories", 1.99m));

        var poster = new LedgerPostingService(db, null, [new EntitlementPurchaseResolver()]);
        await poster.PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);

        // One debit for the total, one credit PER LINE — a single credit could not carry a
        // truthful per-meter tag, leaving the liability unreconcilable by meter.
        lines.Should().HaveCount(3);
        lines.Should().ContainSingle(l => l.Direction == "Debit" && l.Amount == 5.98m);

        var credits = lines.Where(l => l.Direction == "Credit").ToList();
        credits.Should().HaveCount(2);
        credits.Should().OnlyContain(l => l.Code == "2210");
        credits.Should().ContainSingle(l => l.Amount == 3.99m && l.Dimensions.Contains("animated-videos"));
        credits.Should().ContainSingle(l => l.Amount == 1.99m && l.Dimensions.Contains("stories"));

        // The credit lands in a LIABILITY: cash received for units not yet consumed is not revenue.
        var account = await db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Code == "2210");
        account.AccountType.Should().Be("Liability");
    }

    [Fact]
    public async Task RoutedSettlement_Should_BeIdempotent()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, "EntitlementPurchase", ("animated-videos", 3.99m));

        var poster = new LedgerPostingService(db, null, [new EntitlementPurchaseResolver()]);
        await poster.PostInvoiceSettlementAsync(invoice);
        await poster.PostInvoiceSettlementAsync(invoice);

        // Same idempotency key as the single-line path, so a retry cannot settle twice by taking a
        // different branch.
        (await db.JournalEntries.CountAsync(e => e.SourceId == invoice.Id)).Should().Be(1);
    }

    [Fact]
    public async Task TwoResolversClaimingOneOrderType_Should_Throw()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, "EntitlementPurchase", ("animated-videos", 3.99m));

        var poster = new LedgerPostingService(db, null,
            [new EntitlementPurchaseResolver(), new EntitlementPurchaseResolver()]);

        var act = async () => await poster.PostInvoiceSettlementAsync(invoice);

        // Ambiguous routing of real revenue is not something to settle by picking the first.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Exactly one must*");
    }
}
