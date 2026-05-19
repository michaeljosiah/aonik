using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Finance.Readers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Readers;

/// <summary>
/// Tests for <see cref="CustomerInvoiceHistoryReader"/> (Spec 027 Phase 0).
/// </summary>
public class CustomerInvoiceHistoryReaderTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => null;
        public bool TryGetCurrentUserId(out Guid userId) { userId = Guid.Empty; return false; }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"InvoiceReaderTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider());
    }

    private static Invoice SeedInvoice(
        Guid tenantId,
        decimal total = 100m,
        string currency = "USD",
        string status = "Open",
        Guid? orderId = null)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CustomerAccountId = Guid.NewGuid(),
            IssueDate = DateTime.UtcNow.AddDays(-1),
            DueDate = DateTime.UtcNow.AddDays(14),
            Currency = currency,
            Subtotal = total,
            Total = total,
            Status = status,
        };
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ReturnRequestedInvoices()
    {
        using var db = CreateDbContext(TenantA);
        var inv1 = SeedInvoice(TenantA, total: 150m);
        var inv2 = SeedInvoice(TenantA, total: 250m);
        db.Invoices.AddRange(inv1, inv2);
        await db.SaveChangesAsync();

        var reader = new CustomerInvoiceHistoryReader(db);

        var results = await reader.GetByIdsAsync(TenantA, [inv1.Id, inv2.Id]);

        results.Should().HaveCount(2);
        results.Select(x => x.InvoiceId).Should().BeEquivalentTo([inv1.Id, inv2.Id]);
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ReturnEmpty_When_NoIdsProvided()
    {
        using var db = CreateDbContext(TenantA);
        var reader = new CustomerInvoiceHistoryReader(db);

        var results = await reader.GetByIdsAsync(TenantA, []);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_Should_NotLeakAcrossTenants()
    {
        using var db = CreateDbContext(TenantA);
        var tenantBInvoice = SeedInvoice(TenantB);
        db.Invoices.Add(tenantBInvoice);
        await db.SaveChangesAsync();

        var reader = new CustomerInvoiceHistoryReader(db);

        var results = await reader.GetByIdsAsync(TenantA, [tenantBInvoice.Id]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_Should_ProjectAllRequiredFields()
    {
        using var db = CreateDbContext(TenantA);
        var orderId = Guid.NewGuid();
        var invoice = SeedInvoice(TenantA, total: 999m, currency: "GBP", status: "Paid", orderId: orderId);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var reader = new CustomerInvoiceHistoryReader(db);

        var results = await reader.GetByIdsAsync(TenantA, [invoice.Id]);

        results.Should().HaveCount(1);
        var item = results[0];
        item.InvoiceId.Should().Be(invoice.Id);
        item.OrderId.Should().Be(orderId);
        item.Status.Should().Be("Paid");
        item.Currency.Should().Be("GBP");
        item.Total.Should().Be(999m);
        item.IssueDate.Should().Be(invoice.IssueDate);
        item.DueDate.Should().Be(invoice.DueDate);
    }
}
