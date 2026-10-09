using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

public sealed class RefundStatusReaderTests
{
    [Fact]
    public async Task Read_Should_ProjectOnlyAppliedSuccess_AndPreserveCapturedTotals()
    {
        using var test = new Fixture();
        await test.SeedAsync();
        var first = test.Add("Succeeded", applied: true, amount: 20m);
        await test.Db.SaveChangesAsync();
        var reader = new RefundStatusReader(test.Db, test.Tenant);

        (await reader.ReadAsync(test.Order.Id)).Should().Be(new OrderRefundStatusDto("PartiallyRefunded", 12m, 8m, 20m));

        var pending = test.Add("Pending", applied: false, amount: 30m, prior: [first]);
        await test.Db.SaveChangesAsync();
        (await reader.ReadAsync(test.Order.Id)).Should().Be(new OrderRefundStatusDto("RefundPending", 12m, 8m, 20m));
        pending.Status = "Succeeded";
        pending.EffectsAppliedAtUtc = DateTime.UtcNow;
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        (await reader.ReadAsync(test.Order.Id)).Should().Be(new OrderRefundStatusDto("Refunded", 30m, 20m, 50m));
        test.Db.ChangeTracker.Entries().Should().BeEmpty("a customer projection must remain read-only");
        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).Amount.Should().Be(50m);
        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).Status.Should().Be("Captured");
    }

    [Theory]
    [InlineData("NeedsReconciliation", true, "NeedsReconciliation")]
    [InlineData("Failed", true, "NeedsReconciliation")]
    [InlineData("Succeeded", false, "NeedsReconciliation")]
    [InlineData("Unknown", false, "RefundPending")]
    [InlineData("Requested", false, "RefundPending")]
    [InlineData("Failed", false, "None")]
    public async Task Read_Should_NotClaimUnverifiedOrCorrectedMoney(string status, bool applied, string expected)
    {
        using var test = new Fixture();
        await test.SeedAsync();
        test.Add(status, applied, 20m);
        await test.Db.SaveChangesAsync();
        (await new RefundStatusReader(test.Db, test.Tenant).ReadAsync(test.Order.Id))
            .Should().Be(new OrderRefundStatusDto(expected, 0m, 0m, 0m));
    }

    [Fact]
    public async Task Read_Should_RecogniseCompletedZeroMoneyPointsAllocations()
    {
        using var test = new Fixture(pointsOnly: true);
        await test.SeedAsync();
        test.Add("Succeeded", applied: true, amount: 0m);
        await test.Db.SaveChangesAsync();
        (await new RefundStatusReader(test.Db, test.Tenant).ReadAsync(test.Order.Id))
            .Should().Be(new OrderRefundStatusDto("Refunded", 0m, 0m, 0m));
    }

    [Theory]
    [InlineData("foreign-refund")]
    [InlineData("foreign-intent")]
    [InlineData("foreign-reader")]
    [InlineData("deleted-refund")]
    [InlineData("deleted-order")]
    [InlineData("missing-order")]
    public async Task Read_Should_NotIncludeMissingForeignOrDeletedRecords(string scenario)
    {
        using var test = new Fixture();
        if (scenario == "foreign-intent") test.Intent.TenantId = Guid.NewGuid();
        await test.SeedAsync();
        var refund = test.Add("Succeeded", applied: true, amount: 50m);
        if (scenario == "foreign-refund") refund.TenantId = Guid.NewGuid();
        if (scenario == "deleted-refund") refund.IsDeleted = true;
        if (scenario == "deleted-order") test.Order.IsDeleted = true;
        await test.Db.SaveChangesAsync();
        var provider = scenario == "foreign-reader" ? new TestTenantProvider(Guid.NewGuid()) : test.Tenant;
        var result = await new RefundStatusReader(test.Db, provider).ReadAsync(scenario == "missing-order" ? Guid.NewGuid() : test.Order.Id);
        result.Should().Be(new OrderRefundStatusDto("None", 0m, 0m, 0m));
    }

    [Fact]
    public async Task Read_Should_ReportUnusableEvidenceWithoutServingPrivateSnapshotOrThrowing()
    {
        using var test = new Fixture();
        await test.SeedAsync();
        var refund = test.Add("Succeeded", applied: true, amount: 50m);
        refund.RequestSnapshotJson = "{}";
        await test.Db.SaveChangesAsync();
        (await new RefundStatusReader(test.Db, test.Tenant).ReadAsync(test.Order.Id))
            .Should().Be(new OrderRefundStatusDto("NeedsReconciliation", 0m, 0m, 0m));
    }

    private sealed class Fixture : IDisposable
    {
        public TestTenantProvider Tenant { get; } = new(Guid.NewGuid());
        public FinanceDbContext Db { get; }
        public Order Order { get; }
        public PaymentIntent Intent { get; }
        private readonly CheckoutRefundSource _source;

        public Fixture(bool pointsOnly = false)
        {
            Db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"RefundStatus_{Guid.NewGuid()}").Options, Tenant);
            Order = new Order { TenantId = Tenant.GetCurrentTenantId(), OrderType = "ProductPurchase", Status = "Completed", AmountIn = pointsOnly ? 0m : 50m, CurrencyIn = "GBP" };
            Intent = new PaymentIntent { TenantId = Order.TenantId, OrderId = Order.Id, Amount = Order.AmountIn, Currency = "GBP", Status = "Captured" };
            var itemId = Guid.NewGuid();
            _source = new(Order.Id, Intent.Id, null, "GBP", Intent.Amount, pointsOnly ? 0m : 20m, pointsOnly ? 0m : 30m,
                [new(itemId.ToString("N"), itemId, "Product", "Original purchase", Intent.Amount, pointsOnly ? 0m : 20m, 0, pointsOnly ? 500 : 0)]);
        }

        public async Task SeedAsync() { Db.Orders.Add(Order); Db.PaymentIntents.Add(Intent); await Db.SaveChangesAsync(); }

        public Refund Add(string status, bool applied, decimal amount, IReadOnlyList<Refund>? prior = null)
        {
            var draft = new RefundDraft("Private operator reason", [new(_source.Components[0].ComponentId,
                Amount: amount == 0 ? null : amount, FullRemaining: amount == 0)]);
            var calculation = RefundCalculation.Calculate(_source, draft, prior?.Select(RefundSnapshot.Read).ToArray() ?? []);
            var request = new RefundRequest(Guid.NewGuid(), draft.Reason, calculation.Preview.Selections, calculation.Preview.Version);
            var snapshot = new RefundSnapshot(_source, request, Guid.NewGuid(), calculation.Components,
                calculation.Preview.CashAmount, calculation.Preview.GiftAmount, null, null, null, null);
            var refund = new Refund { Id = request.RefundId, TenantId = Order.TenantId, PaymentIntentId = Intent.Id, Status = status,
                Amount = calculation.Preview.Total, Currency = "GBP", RequestSnapshotJson = snapshot.Serialize(),
                EffectsAppliedAtUtc = applied ? DateTime.UtcNow : null };
            Db.Refunds.Add(refund);
            return refund;
        }

        public void Dispose() => Db.Dispose();
    }
}
