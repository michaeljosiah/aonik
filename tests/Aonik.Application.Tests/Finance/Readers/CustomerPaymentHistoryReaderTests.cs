using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Finance.Readers;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance.Readers;

/// <summary>
/// Tests for <see cref="CustomerPaymentHistoryReader"/> (Spec 027 Phase 0).
/// </summary>
public class CustomerPaymentHistoryReaderTests
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
            .UseInMemoryDatabase($"PaymentReaderTests_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider());
    }

    private static PaymentIntent SeedIntent(
        Guid tenantId,
        Guid orderId,
        Guid? invoiceId = null,
        decimal amount = 100m,
        string currency = "USD",
        string status = "Authorized")
    {
        return new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency,
            Status = status,
            PayerPartyId = Guid.NewGuid(),
            PurposeType = "OrderFunding",
            PurposeId = orderId,
            PaymentMethodType = "Card",
        };
    }

    [Fact]
    public async Task GetForOrderOrInvoiceAsync_Should_MatchByOrderId()
    {
        using var db = CreateDbContext(TenantA);
        var orderId = Guid.NewGuid();
        var intent = SeedIntent(TenantA, orderId);
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();

        var reader = new CustomerPaymentHistoryReader(db);

        var results = await reader.GetForOrderOrInvoiceAsync(TenantA, [orderId], []);

        results.Should().HaveCount(1);
        results[0].PaymentIntentId.Should().Be(intent.Id);
        results[0].OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task GetForOrderOrInvoiceAsync_Should_MatchByInvoiceId()
    {
        using var db = CreateDbContext(TenantA);
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var intent = SeedIntent(TenantA, orderId, invoiceId: invoiceId);
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();

        var reader = new CustomerPaymentHistoryReader(db);

        // Searching by a *different* order id but the right invoice id should still match.
        var results = await reader.GetForOrderOrInvoiceAsync(TenantA, [Guid.NewGuid()], [invoiceId]);

        results.Should().HaveCount(1);
        results[0].InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public async Task GetForOrderOrInvoiceAsync_Should_ReturnEmpty_When_NoIdsProvided()
    {
        using var db = CreateDbContext(TenantA);
        var reader = new CustomerPaymentHistoryReader(db);

        var results = await reader.GetForOrderOrInvoiceAsync(TenantA, [], []);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForOrderOrInvoiceAsync_Should_NotLeakAcrossTenants()
    {
        using var db = CreateDbContext(TenantA);
        var orderId = Guid.NewGuid();
        var tenantBIntent = SeedIntent(TenantB, orderId);
        db.PaymentIntents.Add(tenantBIntent);
        await db.SaveChangesAsync();

        var reader = new CustomerPaymentHistoryReader(db);

        // Even with the matching order id, the tenant scope must filter the row out.
        var results = await reader.GetForOrderOrInvoiceAsync(TenantA, [orderId], []);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForOrderOrInvoiceAsync_Should_ProjectAllRequiredFields()
    {
        using var db = CreateDbContext(TenantA);
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var intent = SeedIntent(TenantA, orderId, invoiceId: invoiceId, amount: 444m, currency: "EUR", status: "Captured");
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();

        var reader = new CustomerPaymentHistoryReader(db);

        var results = await reader.GetForOrderOrInvoiceAsync(TenantA, [orderId], [invoiceId]);

        results.Should().HaveCount(1);
        var item = results[0];
        item.PaymentIntentId.Should().Be(intent.Id);
        item.OrderId.Should().Be(orderId);
        item.InvoiceId.Should().Be(invoiceId);
        item.Status.Should().Be("Captured");
        item.Amount.Should().Be(444m);
        item.Currency.Should().Be("EUR");
        item.PurposeType.Should().Be("OrderFunding");
        item.PurposeId.Should().Be(orderId);
    }
}
