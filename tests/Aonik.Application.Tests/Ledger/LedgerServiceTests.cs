using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.Finance.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ledger;

public class LedgerServiceTests
{
    // SharedKernel-level fakes now come from Aonik.TestSupport; only the
    // Finance-specific DbContext factory stays local.
    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task CreateAccountAsync_ShouldCreateLedgerAccount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new LedgerService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics());
        var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
        var request = new CreateLedgerAccountRequest(ledger.Id, "Cash", "1000", "Asset");

        // Act
        var result = await service.CreateAccountAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Cash");
        result.Currency.Should().Be("USD");
        result.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var savedAccount = await context.LedgerAccounts.FirstOrDefaultAsync(a => a.Id == result.Id);
        savedAccount.Should().NotBeNull();
        savedAccount!.Name.Should().Be("Cash");
    }

    [Fact]
    public async Task AddJournalEntryAsync_ShouldCreateJournalEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new LedgerService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics());

        var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
        var cashAccount = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Cash", "1000", "Asset"));
        var revenueAccount = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Revenue", "4000", "Income"));

        var entryRequest = new AddJournalEntryRequest(
            ledger.Id,
            "REF-001",
            "Payment received",
            new List<AddJournalEntryLineRequest>
            {
                new(cashAccount.Id, "Debit", 500.00m, "USD", "Cash received"),
                new(revenueAccount.Id, "Credit", 500.00m, "USD", "Recognize revenue")
            });

        // Act
        var result = await service.AddJournalEntryAsync(entryRequest);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.LedgerId.Should().Be(ledger.Id);
        result.Reference.Should().Be("REF-001");
        result.Description.Should().Be("Payment received");
        result.EntryUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.Lines.Should().HaveCount(2);

        var savedEntry = await context.JournalEntries.FirstOrDefaultAsync(e => e.Id == result.Id);
        savedEntry.Should().NotBeNull();
        savedEntry!.LedgerId.Should().Be(ledger.Id);
    }

    [Fact]
    public async Task AddJournalEntryAsync_WithMultipleEntries_ShouldCreateAll()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new LedgerService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics());

        var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
        var cashAccount = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Operating Cash", "1000", "Asset"));
        var expenseAccount = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Office Expense", "6000", "Expense"));

        // Act
        var entry1 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-001",
                "Entry 1",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount.Id, "Credit", 100.00m, "USD", "Cash out"),
                    new(expenseAccount.Id, "Debit", 100.00m, "USD", "Expense")
                }));
        var entry2 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-002",
                "Entry 2",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount.Id, "Credit", 200.00m, "USD", "Cash out"),
                    new(expenseAccount.Id, "Debit", 200.00m, "USD", "Expense")
                }));
        var entry3 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(
                ledger.Id,
                "REF-003",
                "Entry 3",
                new List<AddJournalEntryLineRequest>
                {
                    new(cashAccount.Id, "Credit", 50.00m, "USD", "Cash out"),
                    new(expenseAccount.Id, "Debit", 50.00m, "USD", "Expense")
                }));

        // Assert
        entry1.Lines.Should().HaveCount(2);
        entry2.Lines.Should().HaveCount(2);
        entry3.Lines.Should().HaveCount(2);

        var entries = await context.JournalEntries
            .Where(e => e.LedgerId == ledger.Id)
            .ToListAsync();
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListJournalEntriesAsync_Should_CapAndPage_When_MoreEntriesThanPageSize()
    {
        // Arrange — five balanced entries in one ledger, page size two (issue H10).
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new LedgerService(
            context,
            new TestTenantProvider(tenantId),
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics());

        var ledger = await service.CreateLedgerAsync(new CreateLedgerRequest("USD"));
        var cash = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Cash", "1000", "Asset"));
        var revenue = await service.CreateAccountAsync(new CreateLedgerAccountRequest(ledger.Id, "Revenue", "4000", "Income"));

        for (var i = 0; i < 5; i++)
        {
            await service.AddJournalEntryAsync(new AddJournalEntryRequest(
                ledger.Id,
                $"REF-{i:D3}",
                $"Entry {i}",
                new List<AddJournalEntryLineRequest>
                {
                    new(cash.Id, "Debit", 100.00m, "USD", "in"),
                    new(revenue.Id, "Credit", 100.00m, "USD", "out")
                }));
        }

        // Act
        var page1 = await service.ListJournalEntriesAsync(new ListJournalEntriesRequest(ledger.Id, PageNumber: 1, PageSize: 2));
        var page2 = await service.ListJournalEntriesAsync(new ListJournalEntriesRequest(ledger.Id, PageNumber: 2, PageSize: 2));
        var page3 = await service.ListJournalEntriesAsync(new ListJournalEntriesRequest(ledger.Id, PageNumber: 3, PageSize: 2));

        // Assert — bounded pages that tile the full set with no overlap.
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);

        var allIds = page1.Concat(page2).Concat(page3).Select(e => e.Id).ToList();
        allIds.Should().OnlyHaveUniqueItems("deterministic paging must not repeat an entry across pages");
        allIds.Should().HaveCount(5, "every journal entry must be reachable across the pages");
    }
}
