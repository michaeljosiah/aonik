using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Ledger;
using Aonik.Application.Services.Ledger;
using Aonik.Infrastructure.Persistence;
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
        var service = new LedgerService(context, tenantProvider);
        var request = new CreateLedgerAccountRequest("Cash", "USD");

        // Act
        var result = await service.CreateAccountAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Cash");
        result.Currency.Should().Be("N/A");
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
        var service = new LedgerService(context, tenantProvider);

        // Create account first
        var accountRequest = new CreateLedgerAccountRequest("Revenue", "USD");
        var account = await service.CreateAccountAsync(accountRequest);

        var entryRequest = new AddJournalEntryRequest(
            account.Id,
            500.00m,
            "USD",
            "REF-001",
            "Payment received");

        // Act
        var result = await service.AddJournalEntryAsync(entryRequest);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.AccountId.Should().Be(account.Id);
        result.Amount.Should().Be(500.00m);
        result.Currency.Should().Be("USD");
        result.Reference.Should().Be("REF-001");
        result.Description.Should().Be("Payment received");
        result.EntryUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var savedEntry = await context.JournalEntries.FirstOrDefaultAsync(e => e.Id == result.Id);
        savedEntry.Should().NotBeNull();
        savedEntry!.SourceId.Should().Be(account.Id);
    }

    [Fact]
    public async Task AddJournalEntryAsync_WithMultipleEntries_ShouldCreateAll()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new LedgerService(context, tenantProvider);

        var accountRequest = new CreateLedgerAccountRequest("Operating Account", "USD");
        var account = await service.CreateAccountAsync(accountRequest);

        // Act
        var entry1 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(account.Id, 100.00m, "USD", "REF-001", "Entry 1"));
        var entry2 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(account.Id, 200.00m, "USD", "REF-002", "Entry 2"));
        var entry3 = await service.AddJournalEntryAsync(
            new AddJournalEntryRequest(account.Id, -50.00m, "USD", "REF-003", "Entry 3"));

        // Assert
        entry1.Amount.Should().Be(100.00m);
        entry2.Amount.Should().Be(200.00m);
        entry3.Amount.Should().Be(-50.00m);

        var entries = await context.JournalEntries
            .Where(e => e.SourceId == account.Id)
            .ToListAsync();
        entries.Should().HaveCount(3);
    }
}
