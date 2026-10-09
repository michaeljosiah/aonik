using System.Data.Common;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests.Commerce;

public sealed class DeliveryReservationConcurrencySqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task DueDiscovery_Should_TranslateGuidSeekAndPageEveryDueReservationAcrossTenants()
    {
        RequireSql();
        var firstTenant = await SeedAsync();
        var secondTenant = await SeedAsync();
        // Earlier than the other cases' holds in this class fixture: discovery is deliberately global.
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        firstTenant.Clock.UtcNow = now;
        secondTenant.Clock.UtcNow = now;
        var expected = new List<(Guid ReservationId, Guid TenantId, Guid CartId)>();
        foreach (var (test, count) in new[] { (firstTenant, 27), (secondTenant, 28) })
        {
            await using var seed = Context(test);
            var poolId = await seed.DeliveryDateCapacities.Where(p => p.DeliveryDate == test.Date).Select(p => p.Id).SingleAsync();
            CartDeliveryReservation Row(string status, DateTime expiresAt, DateTime? deadline = null, bool deleted = false) => new()
            {
                TenantId = test.TenantId, CartId = Guid.NewGuid(), CapacityId = poolId,
                DeliveryDate = test.Date, SelectedAtUtc = now.AddMinutes(-15), ExpiresAtUtc = expiresAt,
                Status = status, PaymentDeadlineUtc = deadline, IsDeleted = deleted
            };
            for (var index = 0; index < count; index++)
            {
                var row = index % 2 == 0
                    ? Row(DeliveryReservationStatuses.Held, now)
                    : Row(DeliveryReservationStatuses.PaymentPending, now.AddMinutes(-5), now.AddSeconds(-1));
                seed.CartDeliveryReservations.Add(row);
                expected.Add((row.Id, row.TenantId, row.CartId));
            }
            seed.CartDeliveryReservations.AddRange(
                Row(DeliveryReservationStatuses.Held, now.AddSeconds(1)),
                Row(DeliveryReservationStatuses.PaymentPending, now.AddMinutes(-5), now.AddSeconds(1)),
                Row(DeliveryReservationStatuses.PaymentPending, now.AddMinutes(-5)),
                Row(DeliveryReservationStatuses.Committed, now.AddMinutes(-5), now.AddMinutes(-1)),
                Row(DeliveryReservationStatuses.Released, now.AddMinutes(-5), now.AddMinutes(-1)),
                Row(DeliveryReservationStatuses.Held, now.AddMinutes(-5), deleted: true));
            await seed.SaveChangesAsync();
        }
        await using var db = Context(firstTenant);
        var tenant = new TestTenantProvider(firstTenant.TenantId);
        var checkout = new CheckoutService(db, Mock.Of<IInventoryService>(MockBehavior.Strict),
            Mock.Of<IOrderService>(MockBehavior.Strict), Mock.Of<IPaymentInitiator>(MockBehavior.Strict),
            Mock.Of<IInvoiceWriter>(MockBehavior.Strict), Mock.Of<IDiscountService>(MockBehavior.Strict),
            Mock.Of<ITaxCalculator>(MockBehavior.Strict), tenant, new UnexpectedBoxCheckout(),
            new GuestOrderAccess(new EphemeralDataProtectionProvider()), Mock.Of<IFulfilmentPromiseService>(MockBehavior.Strict),
            Mock.Of<IDeliveryCoverageService>(MockBehavior.Strict), Mock.Of<IPartyService>(MockBehavior.Strict), firstTenant.Clock,
            Mock.Of<Aonik.SharedKernel.Abstractions.Settings.ITenantSettingStore>());

        var first = await checkout.FindDueDeliveryReservationsAsync();
        var second = await checkout.FindDueDeliveryReservationsAsync(first[^1].ReservationId);
        var end = await checkout.FindDueDeliveryReservationsAsync(second[^1].ReservationId);

        first.Should().HaveCount(50);
        second.Should().HaveCount(5);
        first.Concat(second).Should().BeEquivalentTo(expected);
        first.Concat(second).Select(row => row.ReservationId).Should().OnlyHaveUniqueItems();
        first.Concat(second).Select(row => row.TenantId).Distinct().Should().BeEquivalentTo(
            new[] { firstTenant.TenantId, secondTenant.TenantId });
        end.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Reserve_Should_AdmitOneOfTwoCartsToLastSlot_WithoutLeakingLosingDraft()
    {
        RequireSql();
        var test = await SeedAsync();
        var barrier = new TwoReads("CartDeliveryReservations", countOnly: true);
        await using var firstDb = Context(test, barrier);
        await using var secondDb = Context(test, barrier);

        var first = Service(firstDb, test).ReserveAsync(test.First.Id, test.Date, test.First.Access);
        var second = Service(secondDb, test).ReserveAsync(test.Second.Id, test.Date, test.Second.Access);
        var failures = await Task.WhenAll(Capture(first), Capture(second)).WaitAsync(TimeSpan.FromSeconds(45));

        failures.Count(error => error is null).Should().Be(1);
        var failure = failures.Single(error => error is not null).Should().BeOfType<DeliveryReservationException>().Subject;
        failure.Code.Should().BeOneOf(DeliveryReservationException.Full, DeliveryReservationException.Conflict);
        await firstDb.SaveChangesAsync();
        await secondDb.SaveChangesAsync();
        await using var verify = Context(test);
        var hold = (await verify.CartDeliveryReservations.ToListAsync()).Should().ContainSingle().Subject;
        hold.RowVersion.Should().HaveCount(8);
        var winner = failures[0] is null ? test.First : test.Second;
        var loser = failures[0] is null ? test.Second : test.First;
        hold.CartId.Should().Be(winner.Id);
        hold.Status.Should().Be(DeliveryReservationStatuses.Held);
        CartDraftData.Read(await verify.Carts.SingleAsync(c => c.Id == winner.Id))!.DeliveryDate.Should().Be(test.Date);
        var losingCart = await verify.Carts.SingleAsync(c => c.Id == loser.Id);
        CartDraftData.Read(losingCart)!.DeliveryDate.Should().BeNull();
        CartDraftData.Read(losingCart)!.Notes.Should().Be("Original details");
        Convert.ToBase64String(losingCart.RowVersion).Should().Be(loser.Version);
        var capacity = (await Service(verify, test).GetCapacitiesAsync(test.Date, 1)).Single();
        capacity.Occupied.Should().Be(1);
        capacity.Capacity.Should().Be(1);
        capacity.Version.Should().NotBe(test.OriginalPoolVersion);
    }

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Replace_Should_KeepOldHoldDraftAndBothPools_WhenTargetFullOrSaveFails(bool targetFull)
    {
        RequireSql();
        var test = await SeedAsync();
        var failure = new FailReplacementSave();
        await using var db = Context(test, failure);
        var service = Service(db, test);
        var original = await service.ReserveAsync(test.First.Id, test.Date, test.First.Access);
        if (targetFull)
            await service.ReserveAsync(test.Second.Id, test.Date.AddDays(1), test.Second.Access);
        var poolsBefore = await db.DeliveryDateCapacities.AsNoTracking().ToDictionaryAsync(p => p.Id, p => Convert.ToBase64String(p.RowVersion));
        failure.Armed = !targetFull;
        var replace = () => service.ReserveAsync(test.First.Id, test.Date.AddDays(1),
            CartAccessContext.ForGuest(test.First.Token, original.CartVersion));

        if (targetFull)
            (await replace.Should().ThrowAsync<DeliveryReservationException>()).Which.Code.Should().Be(DeliveryReservationException.Full);
        else
            await replace.Should().ThrowAsync<InjectedReplacementFailure>();

        // An unrelated write in this same scope must not retry either half of a failed move.
        var product = await db.Products.SingleAsync(p => p.Id == test.BundleId);
        product.Name = "Unrelated successful write";
        await db.SaveChangesAsync();
        await using var verify = Context(test);
        var persisted = await Service(verify, test).GetAsync(test.First.Id, test.First.Access);
        persisted.Reservation.Should().Be(original.Reservation);
        persisted.CartVersion.Should().Be(original.CartVersion);
        var draft = CartDraftData.Read(await verify.Carts.SingleAsync(c => c.Id == test.First.Id));
        draft!.DeliveryDate.Should().Be(test.Date);
        draft.Notes.Should().Be("Original details");
        (await verify.DeliveryDateCapacities.ToDictionaryAsync(p => p.Id, p => Convert.ToBase64String(p.RowVersion)))
            .Should().BeEquivalentTo(poolsBefore);
        (await verify.Products.SingleAsync(p => p.Id == test.BundleId)).Name.Should().Be("Unrelated successful write");
        var counts = await Service(verify, test).GetCapacitiesAsync(test.Date, 2);
        counts.Select(c => c.Occupied).Should().Equal(1, targetFull ? 1 : 0);
    }

    [SkippableFact]
    public async Task Replace_Should_ReadIncreasedTargetCapacity_WhenRetryingInSameContextAfterFull()
    {
        RequireSql();
        var test = await SeedAsync();
        var target = test.Date.AddDays(1);
        await using var db = Context(test);
        var service = Service(db, test);
        var original = await service.ReserveAsync(test.First.Id, test.Date, test.First.Access);
        await service.ReserveAsync(test.Second.Id, target, test.Second.Access);
        var access = CartAccessContext.ForGuest(test.First.Token, original.CartVersion);
        var rejected = () => service.ReserveAsync(test.First.Id, target, access);
        (await rejected.Should().ThrowAsync<DeliveryReservationException>()).Which.Code.Should().Be(DeliveryReservationException.Full);
        await using (var adminDb = Context(test))
        {
            var admin = Service(adminDb, test);
            var capacity = (await admin.GetCapacitiesAsync(target, 1)).Single();
            await admin.UpdateCapacityAsync(target, new UpdateDeliveryDateCapacityRequest("box", 2, capacity.Version));
        }

        var moved = await service.ReserveAsync(test.First.Id, target, access);

        moved.Reservation!.Id.Should().Be(original.Reservation!.Id);
        moved.Reservation.DeliveryDate.Should().Be(target);
        moved.CartVersion.Should().NotBe(original.CartVersion);
        await using var verify = Context(test);
        var capacities = await Service(verify, test).GetCapacitiesAsync(test.Date, 2);
        capacities.Select(c => c.Occupied).Should().Equal(0, 2);
        CartDraftData.Read(await verify.Carts.SingleAsync(c => c.Id == test.First.Id))!.DeliveryDate.Should().Be(target);
    }

    [SkippableFact]
    public async Task HeldExpiryRacingPaymentPromotion_Should_KeepExactlyOneValidTransition()
    {
        RequireSql();
        var test = await SeedAsync();
        await using (var seed = Context(test))
            await Service(seed, test).ReserveAsync(test.First.Id, test.Date, test.First.Access);
        var paymentClock = new MutableClock { UtcNow = test.Clock.UtcNow.AddMinutes(14) };
        var expiryClock = new MutableClock { UtcNow = test.Clock.UtcNow.AddMinutes(15) };
        var attemptId = Guid.NewGuid();
        var barrier = new TwoCartSaves();
        await using var paymentDb = Context(test, barrier, paymentClock);
        await using var expiryDb = Context(test, barrier, expiryClock);
        var promotion = TrackedWriteAsync(paymentDb, test, paymentClock, async (service, cart) =>
        {
            await service.BeginPaymentTrackedAsync(cart, test.Date, attemptId);
            cart.CheckoutState = CartCheckoutStates.Preparing;
            return true;
        }, System.Data.IsolationLevel.ReadCommitted);
        var expiry = TrackedWriteAsync(expiryDb, test, expiryClock,
            (service, cart) => service.ExpireHeldTrackedAsync(cart));

        var failures = await Task.WhenAll(Capture(promotion), Capture(expiry)).WaitAsync(TimeSpan.FromSeconds(45));

        await using var verify = Context(test, clock: expiryClock);
        var hold = await verify.CartDeliveryReservations.SingleAsync();
        hold.RowVersion.Should().HaveCount(8);
        var cart = await verify.Carts.SingleAsync(c => c.Id == test.First.Id);
        CartDraftData.Read(cart)!.DeliveryDate.Should().Be(test.Date);
        cart.LastActivityAtUtc.Should().Be(test.Clock.UtcNow);
        var occupied = (await Service(verify, test, expiryClock).GetCapacitiesAsync(test.Date, 1)).Single().Occupied;
        if (hold.Status == DeliveryReservationStatuses.PaymentPending)
        {
            failures[0].Should().BeNull();
            hold.PaymentAttemptId.Should().Be(attemptId);
            hold.PaymentDeadlineUtc.Should().Be(paymentClock.UtcNow.AddMinutes(10));
            occupied.Should().Be(1);
            if (failures[1] is null) (await expiry).Should().BeFalse();
            else failures[1].Should().BeOfType<DbUpdateConcurrencyException>();
        }
        else
        {
            hold.Status.Should().Be(DeliveryReservationStatuses.Released);
            failures[1].Should().BeNull();
            failures[0].Should().NotBeNull();
            if (failures[0] is DeliveryReservationException error) error.Code.Should().Be(DeliveryReservationException.Expired);
            else failures[0].Should().BeOfType<DbUpdateConcurrencyException>();
            hold.PaymentAttemptId.Should().BeNull();
            cart.CheckoutState.Should().BeNull();
            occupied.Should().Be(0);
        }
    }

    [SkippableFact]
    public async Task DuePaymentCommitRacingHeldExpiry_Should_KeepCommittedCapacityAfterBothDeadlines()
    {
        RequireSql();
        var test = await SeedAsync();
        Guid reservationId;
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        DateTime? deadline = null;
        await using (var seed = Context(test))
        {
            var reserved = await Service(seed, test).ReserveAsync(test.First.Id, test.Date, test.First.Access);
            reservationId = reserved.Reservation!.Id;
            await TrackedWriteAsync(seed, test, test.Clock, async (service, cart) =>
            {
                var hold = await service.BeginPaymentTrackedAsync(cart, test.Date, attemptId);
                deadline = hold.PaymentDeadlineUtc;
                await service.BindOrderTrackedAsync(cart, hold.Id, attemptId, orderId, test.Date);
                cart.CheckoutState = CartCheckoutStates.AwaitingPayment;
                cart.OrderId = orderId;
                return true;
            });
        }
        test.Clock.UtcNow = test.Clock.UtcNow.AddHours(1);
        var barrier = new TwoReads("Carts");
        await using var paymentDb = Context(test, barrier);
        await using var expiryDb = Context(test, barrier);
        var commit = TrackedWriteAsync(paymentDb, test, test.Clock, async (service, cart) =>
        {
            await service.CommitTrackedAsync(cart, reservationId, attemptId, orderId, test.Date);
            cart.Status = CartStatuses.CheckedOut;
            return true;
        });
        var expiry = TrackedWriteAsync(expiryDb, test, test.Clock,
            (service, cart) => service.ExpireHeldTrackedAsync(cart));

        await Task.WhenAll(commit, expiry).WaitAsync(TimeSpan.FromSeconds(45));

        (await expiry).Should().BeFalse();
        await using var verify = Context(test);
        var held = await verify.CartDeliveryReservations.SingleAsync();
        held.Status.Should().Be(DeliveryReservationStatuses.Committed);
        held.PaymentDeadlineUtc.Should().Be(deadline);
        held.PaymentAttemptId.Should().Be(attemptId);
        (await Service(verify, test).GetCapacitiesAsync(test.Date, 1)).Single().Occupied.Should().Be(1);
        (await verify.Carts.SingleAsync(c => c.Id == test.First.Id)).Status.Should().Be(CartStatuses.CheckedOut);
    }

    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");

    private CommerceDbContext Context(Harness test, IInterceptor? interceptor = null, IClock? clock = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>().UseSqlServer(database.ConnectionString,
            sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new CommerceDbContext(options.Options, new TestTenantProvider(test.TenantId), null, clock ?? test.Clock);
    }

    private static DeliveryReservationService Service(CommerceDbContext db, Harness test, IClock? clock = null)
        => new(db, new TestTenantProvider(test.TenantId), clock ?? test.Clock);

    // Mirrors the documented tracked-helper contract: one parent cart claim and the hold/pool
    // changes share a short transaction. Financial/provider work is deliberately outside this fixture.
    private static async Task<bool> TrackedWriteAsync(CommerceDbContext db, Harness test, IClock clock,
        Func<DeliveryReservationService, Cart, Task<bool>> mutation,
        System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.Serializable)
    {
        var service = Service(db, test, clock);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                service.Detach(test.First.Id);
                CartTracking.Detach(db, test.TenantId, test.First.Id);
                await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel);
                var cart = await db.Carts.SingleAsync(c => c.TenantId == test.TenantId && c.Id == test.First.Id);
                var changed = await mutation(service, cart);
                if (changed)
                {
                    CartActivity.ServerEdit(db, cart, clock);
                    await db.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                return changed;
            });
        }
        finally
        {
            service.Detach(test.First.Id);
            CartTracking.Detach(db, test.TenantId, test.First.Id);
        }
    }

    private async Task<Harness> SeedAsync()
    {
        var test = new Harness();
        await using var db = Context(test);
        var bundle = new Product { TenantId = test.TenantId, Name = "Delivery test box", Slug = "delivery-test-box",
            Kind = ProductKinds.Bundle, Status = ProductStatuses.Active, BundleCurrency = "GBP" };
        db.Products.Add(bundle);
        test.BundleId = bundle.Id;
        Cart AddCart()
        {
            var cart = new Cart { TenantId = test.TenantId, Currency = "GBP", Status = CartStatuses.Open,
                AnonymousToken = CartAccess.MintToken(), BoxBundleProductId = bundle.Id, BoxSize = 6,
                LastActivityAtUtc = test.Clock.UtcNow,
                CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(Notes: "Original details")) };
            db.Carts.Add(cart);
            return cart;
        }
        var first = AddCart();
        var second = AddCart();
        var originalPool = new DeliveryDateCapacity { TenantId = test.TenantId, DeliveryDate = test.Date, Unit = "box", Capacity = 1 };
        db.DeliveryDateCapacities.AddRange(originalPool,
            new DeliveryDateCapacity { TenantId = test.TenantId, DeliveryDate = test.Date.AddDays(1), Unit = "box", Capacity = 1 });
        db.FulfilmentCalendars.Add(new FulfilmentCalendar { TenantId = test.TenantId, IsActive = true,
            Timezone = "Europe/London", LeadDays = 7, CutoffLocalTime = new TimeOnly(23, 59), DeliveryDaysJson = "[\"friday\",\"saturday\"]" });
        await db.SaveChangesAsync();
        test.First = new(first.Id, first.AnonymousToken!, Convert.ToBase64String(first.RowVersion));
        test.Second = new(second.Id, second.AnonymousToken!, Convert.ToBase64String(second.RowVersion));
        test.OriginalPoolVersion = Convert.ToBase64String(originalPool.RowVersion);
        return test;
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try { await task; return null; }
        catch (Exception error) { return error; }
    }

    private sealed class Harness
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public MutableClock Clock { get; } = new() { UtcNow = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc) };
        public DateOnly Date { get; } = new(2026, 10, 16);
        public Guid BundleId { get; set; }
        public SeededCart First { get; set; } = null!;
        public SeededCart Second { get; set; } = null!;
        public string OriginalPoolVersion { get; set; } = string.Empty;
    }

    private sealed record SeededCart(Guid Id, string Token, string Version)
    {
        public CartAccessContext Access => CartAccessContext.ForGuest(Token, Version);
    }

    private sealed class MutableClock : IClock { public DateTime UtcNow { get; set; } }
    private sealed class InjectedReplacementFailure : Exception { }
    private sealed class UnexpectedBoxCheckout : IBoxCheckoutSupport
    {
        public Task<BoxCheckoutShape> PrepareForCheckoutAsync(Cart cart, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Reservation discovery must not prepare checkout.");
    }

    private sealed class FailReplacementSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<CartDeliveryReservation>().Any(e =>
                    e.State == EntityState.Modified && e.Property(r => r.CapacityId).OriginalValue != e.Entity.CapacityId))
            {
                Armed = false;
                throw new InjectedReplacementFailure();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TwoReads(string table, bool countOnly = false) : DbCommandInterceptor
    {
        private int _arrived;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(table, StringComparison.Ordinal)
                && (!countOnly || command.CommandText.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)))
            {
                var arrived = Interlocked.Increment(ref _arrived);
                if (arrived == 2) _both.TrySetResult();
                if (arrived <= 2) await _both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class TwoCartSaves : SaveChangesInterceptor
    {
        private int _arrived;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(e => e.State == EntityState.Modified))
            {
                var arrived = Interlocked.Increment(ref _arrived);
                if (arrived == 2) _both.TrySetResult();
                if (arrived <= 2) await _both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
