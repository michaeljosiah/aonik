using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Models.Ledger;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Ledger;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ledger;

public class LedgerServiceTests
{
    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
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

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static AonikDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AonikDbContext(options, new TestTenantProvider(tenantId));
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
            new TestCurrentUserProvider(Guid.NewGuid()));
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
            new TestCurrentUserProvider(Guid.NewGuid()));

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
            new TestCurrentUserProvider(Guid.NewGuid()));

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
}
