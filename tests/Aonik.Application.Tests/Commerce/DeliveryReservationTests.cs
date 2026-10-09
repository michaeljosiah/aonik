using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public sealed class DeliveryReservationTests
{
    [Fact]
    public async Task Reserve_Should_SaveDateAndOriginalFifteenMinutes_WithoutRenewingOnReplay()
    {
        using var test = await Harness.CreateAsync();
        var first = await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        test.Clock.UtcNow = test.Clock.UtcNow.AddMinutes(4);
        var replay = await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());

        replay.Reservation.Should().Be(first.Reservation);
        replay.Reservation!.ExpiresAtUtc.Should().Be(first.ServerNowUtc.AddMinutes(15));
        var cart = await test.Db.Carts.AsNoTracking().SingleAsync();
        CartDraftData.Read(cart)!.DeliveryDate.Should().Be(test.Date);
        cart.LastActivityAtUtc.Should().Be(first.ServerNowUtc);
        (await test.Db.CartDeliveryReservations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Replace_Should_KeepTheOriginalHoldAndDraft_WhenNewDateIsFull()
    {
        using var test = await Harness.CreateAsync();
        var first = await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        var next = test.Date.AddDays(1);
        test.Db.DeliveryDateCapacities.Add(new DeliveryDateCapacity { TenantId = test.TenantId, DeliveryDate = next, Unit = "box", Capacity = 0 });
        await test.Db.SaveChangesAsync();

        var act = () => test.Service.ReserveAsync(test.CartId, next, test.Access);
        (await act.Should().ThrowAsync<DeliveryReservationException>()).Which.Code.Should().Be(DeliveryReservationException.Full);

        (await test.Service.GetAsync(test.CartId, test.Access)).Reservation.Should().Be(first.Reservation);
        CartDraftData.Read(await test.Db.Carts.AsNoTracking().SingleAsync())!.DeliveryDate.Should().Be(test.Date);
    }

    [Fact]
    public async Task Replace_Should_FreeOldDateAndStartNewHold_ThenReleaseDateWithoutOtherDetails()
    {
        using var test = await Harness.CreateAsync();
        await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        var next = test.Date.AddDays(1);
        test.Db.DeliveryDateCapacities.Add(new DeliveryDateCapacity { TenantId = test.TenantId, DeliveryDate = next, Unit = "box", Capacity = 1 });
        await test.Db.SaveChangesAsync();
        test.Clock.UtcNow = test.Clock.UtcNow.AddMinutes(2);

        var replaced = await test.Service.ReserveAsync(test.CartId, next, await test.AccessAsync());
        replaced.Reservation!.ExpiresAtUtc.Should().Be(test.Clock.UtcNow.AddMinutes(15));
        (await test.Service.GetCapacitiesAsync(test.Date, 2)).Select(c => c.Occupied).Should().Equal(0, 1);
        await test.Service.ReleaseAsync(test.CartId, await test.AccessAsync());

        var draft = CartDraftData.Read(await test.Db.Carts.AsNoTracking().SingleAsync());
        draft!.DeliveryDate.Should().BeNull();
        draft.Notes.Should().Be("Leave with neighbour");
        (await test.Service.GetCapacitiesAsync(test.Date, 2)).Should().OnlyContain(c => c.Occupied == 0);
    }

    [Fact]
    public async Task ExpiredHeld_Should_FreeAvailability_WithoutDeletingDraftOrTouchingActivity()
    {
        using var test = await Harness.CreateAsync();
        var held = await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        test.Clock.UtcNow = held.Reservation!.ExpiresAtUtc;

        var read = await test.Service.GetAsync(test.CartId, test.Access);
        read.Reservation!.Status.Should().Be("Expired");
        (await test.Service.GetCapacitiesAsync(test.Date, 1)).Single().Occupied.Should().Be(0);
        var cart = await test.Db.Carts.SingleAsync();
        await test.Service.ExpireHeldTrackedAsync(cart);
        await test.Db.SaveChangesAsync();

        CartDraftData.Read(cart)!.DeliveryDate.Should().Be(test.Date);
        cart.LastActivityAtUtc.Should().Be(held.ServerNowUtc);
        (await test.Db.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Released);
    }

    [Fact]
    public async Task PaymentPromotion_Should_FixTenMinuteDeadline_AndKeepUnknownCapacityAfterDeadline()
    {
        using var test = await Harness.CreateAsync();
        await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        test.Clock.UtcNow = test.Clock.UtcNow.AddMinutes(14);
        var cart = await test.Db.Carts.SingleAsync();
        var attempt = Guid.NewGuid();
        var hold = await test.Service.BeginPaymentTrackedAsync(cart, test.Date, attempt);
        var deadline = hold.PaymentDeadlineUtc;
        await test.Db.SaveChangesAsync();
        test.Clock.UtcNow = test.Clock.UtcNow.AddMinutes(1);
        (await test.Service.BeginPaymentTrackedAsync(cart, test.Date, attempt)).PaymentDeadlineUtc.Should().Be(deadline);
        test.Clock.UtcNow = deadline!.Value;

        await test.Service.Invoking(s => s.BeginPaymentTrackedAsync(cart, test.Date, attempt)).Should().ThrowAsync<DeliveryReservationException>();
        (await test.Service.GetCapacitiesAsync(test.Date, 1)).Single().Occupied.Should().Be(1);
        (await test.Service.ExpireHeldTrackedAsync(cart)).Should().BeFalse();
        (await test.Service.GetAsync(test.CartId, test.Access)).Reservation!.Status.Should().Be(DeliveryReservationStatuses.PaymentPending);
    }

    [Fact]
    public async Task OwningFinalSlot_Should_PassCalendarValidation_AndCommitOnlyTheExactPayment()
    {
        using var test = await Harness.CreateAsync();
        await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        var promises = new FulfilmentPromiseService(test.Db, new TestTenantProvider(test.TenantId), test.Clock);
        (await promises.GetDeliveryDatesAsync(test.Date, 1))!.Dates.Should().BeEmpty();
        (await promises.ValidateDeliveryDateAsync(test.Date)).DeliveryDate.Should().Be(test.Date);
        var cart = await test.Db.Carts.SingleAsync();
        var attempt = Guid.NewGuid();
        var order = Guid.NewGuid();
        var hold = await test.Service.BeginPaymentTrackedAsync(cart, test.Date, attempt);
        await test.Service.BindOrderTrackedAsync(cart, hold.Id, attempt, order, test.Date);
        await test.Db.SaveChangesAsync();

        await test.Service.Invoking(s => s.CommitTrackedAsync(cart, hold.Id, Guid.NewGuid(), order, test.Date)).Should().ThrowAsync<DeliveryReservationException>();
        await test.Service.CommitTrackedAsync(cart, hold.Id, attempt, order, test.Date);
        await test.Db.SaveChangesAsync();
        await test.Service.CommitTrackedAsync(cart, hold.Id, attempt, order, test.Date);
        await test.Service.Invoking(s => s.ReleaseTrackedAsync(cart, attempt)).Should().ThrowAsync<DeliveryReservationException>();

        (await test.Service.GetCapacitiesAsync(test.Date, 1)).Single().Occupied.Should().Be(1);
        (await test.Db.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Committed);
    }

    [Fact]
    public async Task PendingRelease_Should_RequireExactAttempt_AndBeIdempotentAfterClosure()
    {
        using var test = await Harness.CreateAsync();
        await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        var cart = await test.Db.Carts.SingleAsync();
        var attempt = Guid.NewGuid();
        await test.Service.BeginPaymentTrackedAsync(cart, test.Date, attempt);
        await test.Db.SaveChangesAsync();

        await test.Service.Invoking(s => s.ReleaseTrackedAsync(cart, null)).Should().ThrowAsync<DeliveryReservationException>();
        await test.Service.Invoking(s => s.ReleaseTrackedAsync(cart, Guid.NewGuid())).Should().ThrowAsync<DeliveryReservationException>();
        await test.Service.ReleaseTrackedAsync(cart, attempt);
        await test.Db.SaveChangesAsync();
        await test.Service.ReleaseTrackedAsync(cart, attempt);

        (await test.Service.GetCapacitiesAsync(test.Date, 1)).Single().Occupied.Should().Be(0);
    }

    [Theory]
    [InlineData("Held")]
    [InlineData("PaymentPending")]
    [InlineData("Committed")]
    public async Task AdminCapacity_Should_NotReduceBelowAnyCountedReservation(string status)
    {
        using var test = await Harness.CreateAsync();
        await test.Service.ReserveAsync(test.CartId, test.Date, await test.AccessAsync());
        var hold = await test.Db.CartDeliveryReservations.SingleAsync();
        hold.Status = status;
        await test.Db.SaveChangesAsync();
        var configured = (await test.Service.GetCapacitiesAsync(test.Date, 1)).Single();

        await test.Service.Invoking(s => s.UpdateCapacityAsync(test.Date, new("box", 0, configured.Version)))
            .Should().ThrowAsync<DeliveryReservationException>();

        (await test.Db.DeliveryDateCapacities.AsNoTracking().SingleAsync()).Capacity.Should().Be(1);
    }

    [Theory]
    [InlineData("wrong-token")]
    [InlineData("foreign-tenant")]
    [InlineData("stale-version")]
    public async Task Reserve_Should_EnforceOwnershipTenantAndVersionBeforeClaiming(string fault)
    {
        using var test = await Harness.CreateAsync();
        var access = await test.AccessAsync();
        var service = test.Service;
        if (fault == "wrong-token") access = access with { GuestToken = CartAccess.MintToken() };
        if (fault == "stale-version") access = access with { ExpectedCartVersion = Convert.ToBase64String(new byte[8]) };
        if (fault == "foreign-tenant") service = new DeliveryReservationService(test.Db, new TestTenantProvider(Guid.NewGuid()), test.Clock);
        var act = () => service.ReserveAsync(test.CartId, test.Date, access);
        if (fault == "stale-version") await act.Should().ThrowAsync<CartWriteConflictException>();
        else await act.Should().ThrowAsync<NotFoundException>();
        (await test.Db.CartDeliveryReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Availability_Should_DistinguishUnknownFullAndCalendarClosure_WithoutGuessingNext()
    {
        using var test = await Harness.CreateAsync();
        var pool = await test.Db.DeliveryDateCapacities.SingleAsync();
        pool.Capacity = 0;
        var calendar = await test.Db.FulfilmentCalendars.SingleAsync();
        calendar.BlackoutDatesJson = $"[\"{test.Date.AddDays(2):yyyy-MM-dd}\"]";
        await test.Db.SaveChangesAsync();
        var promises = new FulfilmentPromiseService(test.Db, new TestTenantProvider(test.TenantId), test.Clock);

        var availability = await promises.GetDeliveryDatesAsync(test.Date, 3);

        availability!.Availability!.Select(d => d.Status).Should().Equal("fully_booked", "unknown", "no_delivery");
        availability.Dates.Should().BeEmpty();
        availability.EarliestDeliveryDate.Should().BeNull();
        await test.Service.Invoking(s => s.ReserveAsync(test.CartId, test.Date.AddDays(1), test.Access))
            .Should().ThrowAsync<DeliveryReservationException>().Where(e => e.Code == DeliveryReservationException.Unavailable);
    }

    private sealed class Harness : IDisposable
    {
        public CommerceDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid CartId { get; } = Guid.NewGuid();
        public DateOnly Date { get; } = new(2026, 6, 25);
        public CommerceTestHarness.TestClock Clock { get; } = new();
        public DeliveryReservationService Service { get; }
        public CartAccessContext Access { get; private set; }

        private Harness()
        {
            var (options, tenantId) = CommerceTestHarness.NewDb();
            TenantId = tenantId;
            Db = CommerceTestHarness.CreateContext(options, tenantId, Clock);
            Service = new DeliveryReservationService(Db, new TestTenantProvider(tenantId), Clock);
            Access = CartAccessContext.ForGuest(CartAccess.MintToken()) with { ExpectedCartVersion = "" };
        }

        public static async Task<Harness> CreateAsync()
        {
            var test = new Harness();
            test.Db.FulfilmentCalendars.Add(new FulfilmentCalendar
            {
                TenantId = test.TenantId, Timezone = "Europe/London", IsActive = true,
                CutoffLocalTime = new TimeOnly(23, 59), DeliveryDaysJson = "[\"monday\",\"tuesday\",\"wednesday\",\"thursday\",\"friday\",\"saturday\",\"sunday\"]"
            });
            test.Db.DeliveryDateCapacities.Add(new DeliveryDateCapacity { TenantId = test.TenantId, DeliveryDate = test.Date, Unit = "box", Capacity = 1 });
            test.Db.Carts.Add(new Cart
            {
                Id = test.CartId, TenantId = test.TenantId, BoxBundleProductId = Guid.NewGuid(), BoxSize = 6,
                AnonymousToken = test.Access.GuestToken, Currency = "GBP",
                CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(Notes: "Leave with neighbour"))
            });
            await test.Db.SaveChangesAsync();
            test.Db.ChangeTracker.Clear();
            return test;
        }

        public async Task<CartAccessContext> AccessAsync()
        {
            Access = Access with { ExpectedCartVersion = Convert.ToBase64String((await Db.Carts.AsNoTracking().SingleAsync()).RowVersion) };
            return Access;
        }

        public void Dispose() => Db.Dispose();
    }
}
