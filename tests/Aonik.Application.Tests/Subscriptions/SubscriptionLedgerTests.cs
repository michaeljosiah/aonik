using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.Subscriptions.Services.Ledger;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using LedgerAccountEntity = Aonik.Finance.Entities.Ledger.LedgerAccount;

namespace Aonik.Application.Tests.Subscriptions;

/// <summary>
/// Spec 087 P5 acceptance: a subscription invoice credits 4100 and an entitlement purchase 2210,
/// each dimensioned by meter.
///
/// This is where the original commercial argument for putting credits on the ledger finally pays
/// off — <b>margin per meter becomes readable from the ledger alone</b>, rather than reconstructed
/// from usage rows in a spreadsheet.
/// </summary>
public class SubscriptionLedgerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private static FinanceDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider());

    private static async Task SeedLedgerAsync(FinanceDbContext db)
    {
        var ledgerId = Guid.NewGuid();
        db.Ledgers.Add(new LedgerEntity { Id = ledgerId, TenantId = TenantId, BaseCurrency = "GBP", IsCanonical = true });

        // 1000/2100/4000 come from the Finance chart; 2210/4100 are contributed by Subscriptions.
        foreach (var (code, name, type) in new[]
                 {
                     ("1000", "Cash", "Asset"),
                     ("2100", "Payments Clearing", "Liability"),
                     ("4000", "Operating Revenue", "Revenue"),
                     ("2210", "Deferred Revenue - Purchased Entitlements", "Liability"),
                     ("4100", "Subscription Revenue", "Revenue")
                 })
        {
            db.LedgerAccounts.Add(new LedgerAccountEntity
            {
                Id = Guid.NewGuid(), TenantId = TenantId, LedgerId = ledgerId,
                Code = code, Name = name, AccountType = type, DimensionsJson = "{}"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Invoice> SeedInvoiceAsync(
        FinanceDbContext db,
        string orderType,
        params (string Description, decimal Amount, string? MeterCode)[] lines)
    {
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order
        {
            Id = orderId, TenantId = TenantId, OrderType = orderType, CurrencyIn = "GBP",
            AmountIn = lines.Sum(l => l.Amount), Status = "Complete", ProvenanceJson = "{}"
        });

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = TenantId, OrderId = orderId,
            CustomerAccountId = Guid.NewGuid(), IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(7),
            Currency = "GBP", Status = "Issued", ProvenanceJson = "{}", Total = lines.Sum(l => l.Amount)
        };
        db.Invoices.Add(invoice);

        foreach (var (description, amount, meterCode) in lines)
        {
            db.InvoiceLines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(), TenantId = TenantId, InvoiceId = invoice.Id,
                Description = description, Quantity = 1, UnitPrice = amount, LineTotal = amount,
                // The link a resolver reads. An invoice line carries no reference to the order line
                // that produced it, so the consumer tags its own lines when it raises the invoice.
                MetadataJson = meterCode is null ? "{}" : $$"""{"meterCode":"{{meterCode}}"}"""
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

        return entry.Lines.Select(l => (accounts[l.LedgerAccountId], l.Direction, l.Amount, l.DimensionsJson)).ToList();
    }

    private static LedgerPostingService Poster(FinanceDbContext db)
        => new(db, null, [new SubscriptionSettlementRevenueResolver()]);

    [Fact]
    public async Task ASubscriptionRenewal_Should_CreditSubscriptionRevenue()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, OrderTypeCodes.SubscriptionRenewal, ("Family plan, August", 19.99m, null));

        await Poster(db).PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "4100" && l.Amount == 19.99m);
    }

    [Fact]
    public async Task AnEntitlementPurchase_Should_CreditALiability_NotRevenue()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, OrderTypeCodes.EntitlementPurchase, ("Extra video", 3.99m, "animated-videos"));

        await Poster(db).PostInvoiceSettlementAsync(invoice);

        var lines = await ReadEntryAsync(db, invoice.Id);

        // Cash received for units not yet consumed is OWED, not earned. Crediting revenue here
        // would overstate earnings and understate what the business still owes its customers.
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "2210" && l.Amount == 3.99m);

        var account = await db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Code == "2210");
        account.AccountType.Should().Be("Liability");
    }

    [Fact]
    public async Task AMultiMeterPurchase_Should_SplitAndDimensionEachLine()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, OrderTypeCodes.EntitlementPurchase,
            ("Extra video", 3.99m, "animated-videos"),
            ("Extra story", 1.99m, "stories"));

        await Poster(db).PostInvoiceSettlementAsync(invoice);

        var credits = (await ReadEntryAsync(db, invoice.Id)).Where(l => l.Direction == "Credit").ToList();

        // A single credit for the total could carry only one meterCode — which would be false for
        // the other line, and would leave the liability unreconcilable per meter.
        credits.Should().HaveCount(2);
        credits.Should().ContainSingle(l => l.Amount == 3.99m && l.Dimensions.Contains("animated-videos"));
        credits.Should().ContainSingle(l => l.Amount == 1.99m && l.Dimensions.Contains("stories"));
    }

    [Fact]
    public async Task AnUnrelatedOrderType_Should_StillCredit4000()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, OrderTypeCodes.BillPayment, ("Electricity bill", 42m, null));

        await Poster(db).PostInvoiceSettlementAsync(invoice);

        // The regression that matters: registering this resolver must not move anything else.
        var lines = await ReadEntryAsync(db, invoice.Id);
        lines.Should().HaveCount(2);
        lines.Should().ContainSingle(l => l.Direction == "Credit" && l.Code == "4000");
    }

    [Fact]
    public async Task ALineWithNoMeterTag_Should_StillSettle_Undimensioned()
    {
        var db = CreateDb();
        await SeedLedgerAsync(db);
        var invoice = await SeedInvoiceAsync(db, OrderTypeCodes.EntitlementPurchase, ("Untagged", 5m, null));

        var act = async () => await Poster(db).PostInvoiceSettlementAsync(invoice);

        // Refusing to settle real money because an analytic tag is missing would be the wrong
        // trade — the entry still balances and still routes correctly.
        await act.Should().NotThrowAsync();
        (await ReadEntryAsync(db, invoice.Id)).Should().ContainSingle(l => l.Code == "2210");
    }

    [Fact]
    public void UnreadableMetadata_Should_YieldNoDimension_RatherThanThrow()
    {
        // MetadataJson is a free-form blob other code may also write to.
        SubscriptionSettlementRevenueResolver.ReadMeterCode("not json at all").Should().BeNull();
        SubscriptionSettlementRevenueResolver.ReadMeterCode("{}").Should().BeNull();
        SubscriptionSettlementRevenueResolver.ReadMeterCode(null).Should().BeNull();
        SubscriptionSettlementRevenueResolver.ReadMeterCode("""{"meterCode":"stories"}""").Should().Be("stories");
    }

    [Fact]
    public void TheContributedAccounts_Should_MatchTheOnesTheResolverRoutesTo()
    {
        var contributed = new SubscriptionLedgerAccountContributor().GetAccounts();

        // If these drift apart, settlement routes to an account provisioning never created and
        // IJournalWriter rejects the post — at settlement time, on real money.
        contributed.Should().Contain(a => a.Code == SubscriptionAccounts.DeferredEntitlements && a.AccountType == "Liability");
        contributed.Should().Contain(a => a.Code == SubscriptionAccounts.SubscriptionRevenue && a.AccountType == "Revenue");
        contributed.Should().Contain(a => a.Code == SubscriptionAccounts.EntitlementRevenue && a.AccountType == "Revenue");
        contributed.Should().Contain(a => a.Code == SubscriptionAccounts.ProviderCost && a.AccountType == "Expense");
    }

    [Fact]
    public void TheContributedAccounts_Should_NotCollideWithTheFinanceChart()
    {
        var contributed = new SubscriptionLedgerAccountContributor().GetAccounts().Select(a => a.Code).ToList();

        // 2200 is Customer Funds Liability from the remittance flow, and 2100 is Payments
        // Clearing — reusing either would silently merge two unrelated balances.
        contributed.Should().NotContain(["1000", "1100", "1300", "2000", "2100", "2150", "2200", "3000", "4000", "5000"]);
    }
}
