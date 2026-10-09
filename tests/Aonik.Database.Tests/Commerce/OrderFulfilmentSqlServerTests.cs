using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Database.Tests.Commerce;

public sealed class OrderFulfilmentSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableFact]
    public async Task CompetingSameVersionWrites_Should_ConvergeToOneTransitionAndNeverReflushLosingHistory()
    {
        RequireSql();
        var seed = await SeedAsync();
        var gate = new TwoWritesGate();
        await using var firstDb = Context(seed.TenantId, gate);
        await using var secondDb = Context(seed.TenantId, gate);
        var firstActor = Guid.NewGuid();
        var secondActor = Guid.NewGuid();
        var first = Service(firstDb, seed, firstActor);
        var second = Service(secondDb, seed, secondActor);
        var command = new UpdateOrderFulfilmentCommand(OrderFulfilmentStatuses.Cooking, seed.Version);
        var one = first.UpdateAsync(seed.OrderId, command);
        var two = second.UpdateAsync(seed.OrderId, command);
        try
        {
            await AwaitBothAsync(gate.BothArrived.Task, one, two);
            gate.Release.TrySetResult();
            var results = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(30));

            results[0].Status.Should().Be(OrderFulfilmentStatuses.Cooking);
            results[1].Should().BeEquivalentTo(results[0]);
            results[0].Version.Should().NotBe(seed.Version);
            Convert.FromBase64String(results[0].Version).Should().HaveCount(8);
            var transition = results[0].History.Should().ContainSingle().Subject;
            transition.FromStatus.Should().Be(OrderFulfilmentStatuses.Confirmed);
            transition.ToStatus.Should().Be(OrderFulfilmentStatuses.Cooking);
            new[] { firstActor, secondActor }.Should().Contain(transition.ActorId);
            transition.OccurredAtUtc.Should().Be(Clock.UtcNow);

            // A losing tracked history must not be persisted by a later unrelated save.
            await firstDb.SaveChangesAsync();
            await secondDb.SaveChangesAsync();
            await using var verify = Context(seed.TenantId);
            var row = await verify.OrderDeliveryDetails.SingleAsync();
            OrderFulfilmentData.Map(row).Should().BeEquivalentTo(results[0]);
            row.PurchaserEmail.Should().Be("purchaser@example.test");
            (await verify.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task ForwardStages_Should_PersistExactlyThreeActorEvents_WhileSameStageReplayDoesNotChangeNativeVersion()
    {
        RequireSql();
        var seed = await SeedAsync();
        var actorId = Guid.NewGuid();
        await using var db = Context(seed.TenantId);
        var service = Service(db, seed, actorId);
        var initial = await service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.Confirmed, seed.Version));
        initial.History.Should().BeEmpty("a legacy paid snapshot has no invented fulfilment event");
        initial.Version.Should().Be(seed.Version);
        var cooking = await service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.Cooking, seed.Version));

        var replay = await service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.Cooking, seed.Version));

        replay.Should().BeEquivalentTo(cooking);
        var staleAdvance = () => service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.OutForDelivery, seed.Version));
        await staleAdvance.Should().ThrowAsync<OrderFulfilmentConflictException>();
        var dispatch = await service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.OutForDelivery, cooking.Version));
        var delivered = await service.UpdateAsync(seed.OrderId,
            new(OrderFulfilmentStatuses.Delivered, dispatch.Version));
        await db.SaveChangesAsync();

        delivered.History.Select(item => item.ToStatus).Should().Equal(
            OrderFulfilmentStatuses.Cooking, OrderFulfilmentStatuses.OutForDelivery, OrderFulfilmentStatuses.Delivered);
        delivered.History.Should().OnlyContain(item => item.ActorId == actorId && item.OccurredAtUtc == Clock.UtcNow);
        await using var verify = Context(seed.TenantId);
        var row = await verify.OrderDeliveryDetails.SingleAsync();
        OrderFulfilmentData.Map(row).Should().BeEquivalentTo(delivered);
        row.FulfilmentHistoryJson!.Length.Should().BeLessThanOrEqualTo(4000);
        row.RowVersion.Should().HaveCount(8);
    }

    [SkippableFact]
    public async Task ForeignTenantAndMissingActor_Should_RejectWithoutReadingSpineOrChangingDelivery()
    {
        RequireSql();
        var seed = await SeedAsync();
        var unreadOrders = new Mock<IOrderService>(MockBehavior.Strict);
        var command = new UpdateOrderFulfilmentCommand(OrderFulfilmentStatuses.Cooking, seed.Version);
        var foreignTenant = Guid.NewGuid();
        await using var foreignDb = Context(foreignTenant);
        var foreign = new OrderFulfilmentService(foreignDb, new TestTenantProvider(foreignTenant),
            new TestCurrentUserProvider(), Clock, unreadOrders.Object);
        var foreignWrite = () => foreign.UpdateAsync(seed.OrderId, command);
        await foreignWrite.Should().ThrowAsync<NotFoundException>();
        await using var ownDb = Context(seed.TenantId);
        var anonymous = new OrderFulfilmentService(ownDb, new TestTenantProvider(seed.TenantId),
            Mock.Of<ICurrentUserProvider>(), Clock, unreadOrders.Object);
        var anonymousWrite = () => anonymous.UpdateAsync(seed.OrderId, command);
        await anonymousWrite.Should().ThrowAsync<UnauthorizedAccessException>();

        await foreignDb.SaveChangesAsync();
        await ownDb.SaveChangesAsync();
        unreadOrders.VerifyNoOtherCalls();
        var unchanged = await ownDb.OrderDeliveryDetails.AsNoTracking().SingleAsync();
        Convert.ToBase64String(unchanged.RowVersion).Should().Be(seed.Version);
        unchanged.FulfilmentStatus.Should().BeNull();
        unchanged.FulfilmentHistoryJson.Should().BeNull();
    }

    private async Task<Seed> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await using var db = Context(tenantId);
        db.Carts.Add(new Cart { TenantId = tenantId, OrderId = orderId, Currency = "GBP", Status = CartStatuses.CheckedOut });
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = tenantId, OrderId = orderId, Currency = "GBP", PaymentIntentId = Guid.NewGuid(),
            Subtotal = 95m, Total = 95m, PaymentStatus = CheckoutPaymentStatuses.Captured
        });
        var delivery = new OrderDeliveryDetails
        {
            TenantId = tenantId, OrderId = orderId, PurchaserEmail = "purchaser@example.test",
            PurchaserFirstName = "Pat", PurchaserLastName = "Customer", PurchaserPhone = "07700900123",
            AddressLine1 = "1 Test Street", City = "London", CountryCode = "GB", Postcode = "SW1A 1AA",
            RecipientName = "Pat Customer", RecipientPhone = "07700900123",
            DeliveryDate = new DateOnly(2026, 10, 16), Timezone = "Europe/London"
        };
        db.OrderDeliveryDetails.Add(delivery);
        await db.SaveChangesAsync();
        return new Seed(tenantId, orderId, Convert.ToBase64String(delivery.RowVersion));
    }

    private CommerceDbContext Context(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
    }
    private static OrderFulfilmentService Service(CommerceDbContext db, Seed seed, Guid actorId)
    {
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        orders.Setup(service => service.GetAsync(seed.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new OrderDto(seed.OrderId, seed.TenantId, OrderTypeCodes.ProductPurchase, OrderStatusCodes.Complete,
                null, 95m, "GBP", Clock.UtcNow, []));
        return new(db, new TestTenantProvider(seed.TenantId), new TestCurrentUserProvider(actorId), Clock, orders.Object);
    }
    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
    private static async Task AwaitBothAsync(Task gate, Task first, Task second)
    {
        var completed = await Task.WhenAny(gate, first, second).WaitAsync(TimeSpan.FromSeconds(30));
        if (completed != gate) await completed;
        await gate.WaitAsync(TimeSpan.FromSeconds(30));
    }
    private sealed record Seed(Guid TenantId, Guid OrderId, string Version);
    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class TwoWritesGate : SaveChangesInterceptor
    {
        private int _arrivals;
        public TaskCompletionSource BothArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Release.Task.IsCompleted && eventData.Context!.ChangeTracker.Entries<OrderDeliveryDetails>()
                    .Any(entry => entry.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) BothArrived.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
