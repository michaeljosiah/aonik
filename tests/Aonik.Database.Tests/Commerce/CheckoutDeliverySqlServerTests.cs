using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Ordering;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Database.Tests.Commerce;

/// <summary>Real SQL claims and snapshot uniqueness; checkout and inventory deliberately share
/// one Commerce context, as they do in production. Provider calls alone are test doubles.</summary>
public class CheckoutDeliverySqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly DateOnly FirstDate = new(2026, 10, 16);
    private static readonly FixedClock Clock = new();

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompetingCheckouts_Should_ReplayTheWinner_AndKeepItsOriginalDeliveryChoiceAndHold(bool winnerHasDelivery)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scopeA = provider.CreateAsyncScope();
        await using var scopeB = provider.CreateAsyncScope();
        var gateA = new BeforeCheckoutSave();
        var gateB = new BeforeCheckoutSave();
        await using var contextA = Commerce(tenantId, gateA);
        await using var contextB = Commerce(tenantId, gateB);
        var paymentA = new RecordingPayment();
        var paymentB = new RecordingPayment();
        var checkoutA = Checkout(contextA, scopeA.ServiceProvider, tenantId, paymentA);
        var checkoutB = Checkout(contextB, scopeB.ServiceProvider, tenantId, paymentB);
        var access = CartAccessContext.ForGuest(seeded.Token, seeded.Version);

        // Let both requests validate the original cart and stage their final writes. Sequence
        // the earlier inventory work so this tests the cart claim, not stock-row contention.
        var firstCommand = Command(seeded.CartId, "1 Original Road", FirstDate);
        if (!winnerHasDelivery) firstCommand = firstCommand with { Delivery = null };
        var first = checkoutA.CheckoutAsync(firstCommand, access);
        try
        {
            await gateA.WaitUntilReachedAsync(first);
            var second = checkoutB.CheckoutAsync(Command(seeded.CartId, "2 Losing Road", FirstDate.AddDays(1)), access);
            try
            {
                await gateB.WaitUntilReachedAsync(second);
                gateA.OrderId.Should().Be(gateB.OrderId, "the real order spine deduplicates by cart id");
                gateA.Release.TrySetResult();
                var winner = await first;
                gateB.Release.TrySetResult();
                var replay = await second;

                gateB.Failure.Should().BeAssignableTo<DbUpdateException>("the losing request must exercise SQL conflict recovery");
                replay.OrderId.Should().Be(winner.OrderId);
                replay.PaymentIntentId.Should().Be(winner.PaymentIntentId).And.Be(paymentA.IntentId);
                replay.ClientSecret.Should().Be(winner.ClientSecret);
                replay.Total.Should().Be(winner.Total);
                paymentA.Calls.Should().Be(1);
                paymentB.Calls.Should().Be(1);
                paymentB.IntentId.Should().NotBe(winner.PaymentIntentId);

                // A later SaveChanges on the losing scope must not retry its rejected inserts.
                await contextB.SaveChangesAsync();
                await using var verification = Commerce(tenantId);
                var delivery = await verification.OrderDeliveryDetails.AsNoTracking().SingleOrDefaultAsync();
                if (winnerHasDelivery)
                {
                    delivery.Should().NotBeNull();
                    delivery!.OrderId.Should().Be(winner.OrderId);
                    delivery.AddressLine1.Should().Be("1 Original Road");
                    delivery.DeliveryDate.Should().Be(FirstDate);
                    delivery.Timezone.Should().Be("Europe/London");
                    delivery.RowVersion.Should().HaveCount(8);
                }
                else
                {
                    delivery.Should().BeNull("a generic winner can omit delivery; the loser must not add its snapshot");
                }
                (await verification.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(winner.PaymentIntentId);
                (await verification.OrderBundleSelections.CountAsync()).Should().Be(1);
                (await verification.Carts.SingleAsync()).OrderId.Should().Be(winner.OrderId);
                var held = await verification.InventoryReservations.Where(row => row.Status == InventoryReservationStatuses.Held).ToListAsync();
                held.Should().ContainSingle().Which.HoldRef.Should().Be(seeded.CartId);
                (await verification.InventoryLevels.SingleAsync()).Reserved.Should().Be(1m);
                await using var readScope = provider.CreateAsyncScope();
                (await readScope.ServiceProvider.GetRequiredService<IOrderService>().GetAsync(winner.OrderId))!
                    .Status.Should().Be(OrderStatusCodes.Draft, "the loser must not cancel the winner's unpaid order");
                // The schema fixture built an unscoped model. Use a separate provider for these
                // runtime reads so its cached model cannot suppress tenant filters.
                await using var canonicalServices = new ServiceCollection().AddEntityFrameworkSqlServer().BuildServiceProvider();
                var canonicalOptions = new DbContextOptionsBuilder<AonikDbContext>(database.CreateOptions<AonikDbContext>())
                    .UseInternalServiceProvider(canonicalServices).Options;
                await using var fundingRead = new AonikDbContext(canonicalOptions,
                    new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
                var fundingRefs = await fundingRead.OrderFundingRefs.AsNoTracking()
                    .Where(row => row.OrderId == winner.OrderId).ToListAsync();
                fundingRefs.Should().ContainSingle(row => row.PaymentIntentId == winner.PaymentIntentId);
                fundingRefs.Should().OnlyContain(row => row.TenantId == tenantId && row.RowVersion.Length == 8,
                    "the canonical and runtime models must both use SQL-generated funding-reference rowversions");
                await using var foreignFundingRead = new AonikDbContext(canonicalOptions,
                    new TestTenantProvider(Guid.NewGuid()), new TestCurrentUserProvider(), Clock);
                (await foreignFundingRead.OrderFundingRefs.AnyAsync(row => row.OrderId == winner.OrderId))
                    .Should().BeFalse("funding references must remain tenant filtered in the canonical context");
            }
            finally
            {
                gateB.Release.TrySetResult();
            }
        }
        finally
        {
            gateA.Release.TrySetResult();
        }
    }

    [SkippableFact]
    public async Task CartEdit_Should_RollBackSnapshots_AndReleaseInventoryWithoutReflushingFailedWrites()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        var gate = new BeforeCheckoutSave();
        await using var context = Commerce(tenantId, gate);
        var checkout = Checkout(context, scope.ServiceProvider, tenantId, new RecordingPayment());
        var pending = checkout.CheckoutAsync(Command(seeded.CartId, "1 Original Road", FirstDate), CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        try
        {
            await gate.WaitUntilReachedAsync(pending);
            await using (var edit = Commerce(tenantId))
            {
                var cart = await edit.Carts.Include(row => row.Items).SingleAsync();
                var before = cart.RowVersion.ToArray();
                cart.Items.Single().Quantity = 2m;
                cart.UpdatedAt = Clock.UtcNow; // real cart mutations also touch the cart claim row
                await edit.SaveChangesAsync();
                cart.RowVersion.Should().HaveCount(8).And.NotEqual(before);
            }
            gate.Release.TrySetResult();
            Func<Task> act = async () => await pending;
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

            context.ChangeTracker.Entries().Should().NotContain(entry => entry.State == EntityState.Added
                && (entry.Entity is OrderDeliveryDetails || entry.Entity is OrderChargeSummary || entry.Entity is OrderBundleSelection));
            // A real inventory mutation flushes this SAME context again, after cleanup did so.
            await Inventory(context, tenantId).SetOnHandAsync(seeded.VariantId, 25m);
            await using var verification = Commerce(tenantId);
            (await verification.OrderDeliveryDetails.CountAsync()).Should().Be(0);
            (await verification.OrderChargeSummaries.CountAsync()).Should().Be(0);
            (await verification.OrderBundleSelections.CountAsync()).Should().Be(0);
            var savedCart = await verification.Carts.Include(row => row.Items).SingleAsync();
            savedCart.OrderId.Should().BeNull();
            savedCart.Items.Single().Quantity.Should().Be(2m);
            (await verification.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Released);
            var level = await verification.InventoryLevels.SingleAsync();
            level.Reserved.Should().Be(0m);
            level.OnHand.Should().Be(25m);
            await using var readScope = provider.CreateAsyncScope();
            (await readScope.ServiceProvider.GetRequiredService<IOrderService>().GetAsync(gate.OrderId))!
                .Status.Should().Be(OrderStatusCodes.Cancelled);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
    }

    [SkippableFact]
    public async Task DuplicateSnapshotWithoutACompleteWinner_Should_RethrowDatabaseFailure_AndPreserveOriginalData()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        var gate = new BeforeCheckoutSave();
        await using var context = Commerce(tenantId, gate);
        var pending = Checkout(context, scope.ServiceProvider, tenantId, new RecordingPayment())
            .CheckoutAsync(Command(seeded.CartId, "2 Losing Road", FirstDate.AddDays(1)), CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        try
        {
            await gate.WaitUntilReachedAsync(pending);
            // Reproduce an existing snapshot with an incomplete checkout claim. It is an
            // integrity failure, not evidence that this request may replace or cancel it.
            await using (var other = Commerce(tenantId))
            {
                other.OrderDeliveryDetails.Add(Snapshot(tenantId, gate.OrderId, "1 Original Road", FirstDate));
                await other.SaveChangesAsync();
            }
            gate.Release.TrySetResult();
            Func<Task> act = async () => await pending;
            var error = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
            error.Should().NotBeOfType<DbUpdateConcurrencyException>();
            error.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().BeOneOf(2601, 2627);
            await context.SaveChangesAsync();

            await using var verification = Commerce(tenantId);
            (await verification.OrderDeliveryDetails.SingleAsync()).AddressLine1.Should().Be("1 Original Road");
            (await verification.Carts.SingleAsync()).OrderId.Should().BeNull();
            (await verification.OrderChargeSummaries.CountAsync()).Should().Be(0);
            (await verification.OrderBundleSelections.CountAsync()).Should().Be(0);
            await using var readScope = provider.CreateAsyncScope();
            (await readScope.ServiceProvider.GetRequiredService<IOrderService>().GetAsync(gate.OrderId))!
                .Status.Should().Be(OrderStatusCodes.Draft);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
    }

    [SkippableFact]
    public async Task RevokedGuestLosingToAnAdoptedCheckout_Should_NotReplayTheNewOwnersSecretsOrCancelItsOrder()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scopeA = provider.CreateAsyncScope();
        await using var scopeB = provider.CreateAsyncScope();
        var gate = new BeforeCheckoutSave();
        await using var contextA = Commerce(tenantId, gate);
        var guestAccess = CartAccessContext.ForGuest(seeded.Token, seeded.Version);
        var guestAttempt = Checkout(contextA, scopeA.ServiceProvider, tenantId, new RecordingPayment())
            .CheckoutAsync(Command(seeded.CartId, "Guest address", FirstDate), guestAccess);
        try
        {
            await gate.WaitUntilReachedAsync(guestAttempt);
            await using var ownerContext = Commerce(tenantId);
            var tenant = new TestTenantProvider(tenantId);
            var carts = new CartService(ownerContext, tenant, new ProductPricingService(ownerContext, tenant, Clock), Clock);
            var adopted = await carts.AdoptAsync(seeded.CartId, partyId, guestAccess);
            var ownerPayment = new RecordingPayment();
            var winner = await Checkout(ownerContext, scopeB.ServiceProvider, tenantId, ownerPayment)
                .CheckoutAsync(Command(seeded.CartId, "Owner address", FirstDate.AddDays(1)),
                    CartAccessContext.ForParty(partyId, adopted.CartVersion));
            winner.OrderId.Should().Be(gate.OrderId);
            gate.Release.TrySetResult();

            Func<Task> act = async () => await guestAttempt;
            await act.Should().ThrowAsync<NotFoundException>();
            await contextA.SaveChangesAsync();
            await using var verify = Commerce(tenantId);
            var cart = await verify.Carts.SingleAsync();
            cart.BuyerPartyId.Should().Be(partyId);
            cart.AnonymousToken.Should().BeNull();
            cart.OrderId.Should().Be(winner.OrderId);
            (await verify.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(ownerPayment.IntentId);
            (await verify.OrderDeliveryDetails.SingleAsync()).AddressLine1.Should().Be("Owner address");
            (await verify.OrderBundleSelections.CountAsync()).Should().Be(1);
            (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(1m);
            await using var readScope = provider.CreateAsyncScope();
            (await readScope.ServiceProvider.GetRequiredService<IOrderService>().GetAsync(winner.OrderId))!
                .Status.Should().Be(OrderStatusCodes.Draft);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task Checkout_Should_RejectFreshlyObservedStaleVersion_WithoutFlushingItsPreviouslyTrackedGraph()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        await using var stale = Commerce(tenantId);
        var cart = await stale.Carts.Include(row => row.Items).ThenInclude(row => row.Selections).SingleAsync();
        cart.Items.Single().Quantity = 50m;
        cart.Items.Single().Selections.Single().Quantity = 50m;
        await using (var writer = Commerce(tenantId))
        {
            var tenant = new TestTenantProvider(tenantId);
            await new CartService(writer, tenant, new ProductPricingService(writer, tenant, Clock), Clock)
                .SaveCheckoutDraftAsync(seeded.CartId, new CartCheckoutDraftDto(Notes: "Newer draft"),
                    CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        }
        var payment = new RecordingPayment();
        var act = () => Checkout(stale, scope.ServiceProvider, tenantId, payment)
            .CheckoutAsync(Command(seeded.CartId, "Old address", FirstDate),
                CartAccessContext.ForGuest(seeded.Token, seeded.Version));

        await act.Should().ThrowAsync<CartWriteConflictException>();
        payment.Calls.Should().Be(0);
        await stale.SaveChangesAsync();
        await using var verify = Commerce(tenantId);
        (await verify.CartItems.SingleAsync()).Quantity.Should().Be(1m);
        (await verify.CartItemSelections.SingleAsync()).Quantity.Should().Be(1m);
        (await verify.InventoryReservations.CountAsync()).Should().Be(0);
        (await verify.OrderChargeSummaries.CountAsync()).Should().Be(0);
        CartDraftData.Read(await verify.Carts.SingleAsync())!.Notes.Should().Be("Newer draft");
    }

    [SkippableFact]
    public async Task FailedPaymentInitiation_Should_NotStageSelectionsThatALaterInventorySaveCouldFlush()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        await using var context = Commerce(tenantId);
        var payment = new RecordingPayment { Fail = true };
        var checkout = Checkout(context, scope.ServiceProvider, tenantId, payment);
        var command = Command(seeded.CartId, "Original address", FirstDate);
        var access = CartAccessContext.ForGuest(seeded.Token, seeded.Version);

        var act = () => checkout.CheckoutAsync(command, access);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test payment failure.");
        await Inventory(context, tenantId).SetOnHandAsync(seeded.VariantId, 25m);
        await using (var verify = Commerce(tenantId))
        {
            (await verify.OrderBundleSelections.CountAsync()).Should().Be(0);
            (await verify.OrderChargeSummaries.CountAsync()).Should().Be(0);
            (await verify.OrderDeliveryDetails.CountAsync()).Should().Be(0);
            (await verify.Carts.SingleAsync()).OrderId.Should().BeNull();
        }
        payment.Fail = false;
        var completed = await checkout.CheckoutAsync(command, access);
        await using var read = Commerce(tenantId);
        (await read.OrderBundleSelections.SingleAsync()).OrderId.Should().Be(completed.OrderId);
    }

    [SkippableFact]
    public async Task DeliverySnapshot_Should_RoundTripDateAndNativeVersion_AndEnforceTenantOrderUniqueness()
    {
        RequireSqlServer();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await using (var first = Commerce(tenantA))
        {
            first.OrderDeliveryDetails.Add(Snapshot(tenantA, orderId, "Tenant A address", FirstDate));
            await first.SaveChangesAsync();
        }
        await using (var foreign = Commerce(tenantB))
        {
            foreign.OrderDeliveryDetails.Add(Snapshot(tenantB, orderId, "Tenant B address", FirstDate.AddDays(1)));
            await foreign.SaveChangesAsync();
            (await foreign.OrderDeliveryDetails.SingleAsync()).AddressLine1.Should().Be("Tenant B address");
        }
        await using var read = Commerce(tenantA);
        var recorded = await read.OrderDeliveryDetails.SingleAsync();
        recorded.DeliveryDate.Should().Be(FirstDate);
        recorded.RowVersion.Should().HaveCount(8);
        recorded.AddressLine1.Should().Be("Tenant A address");
        read.OrderDeliveryDetails.Remove(recorded);
        await read.SaveChangesAsync(); // soft deletion must not permit rewriting the historical snapshot

        await using var duplicate = Commerce(tenantA);
        duplicate.OrderDeliveryDetails.Add(Snapshot(tenantA, orderId, "Replacement", FirstDate.AddDays(2)));
        var act = () => duplicate.SaveChangesAsync();
        var error = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        error.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().BeOneOf(2601, 2627);
    }

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private CommerceDbContext Commerce(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
    }

    private ServiceProvider OrderingProvider(Guid tenantId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Aonik.SharedKernel.Abstractions.Multitenancy.ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider());
        services.AddSingleton<IClock>(Clock);
        services.AddOrderingModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
        }).Build());
        return services.BuildServiceProvider();
    }

    private static InventoryService Inventory(CommerceDbContext context, Guid tenantId) => new(
        context, new TestTenantProvider(tenantId), new TenantContext { TenantId = tenantId }, Clock);

    private static CheckoutService Checkout(CommerceDbContext context, IServiceProvider ordering, Guid tenantId, RecordingPayment payments)
    {
        var tenant = new TestTenantProvider(tenantId);
        return new CheckoutService(context, Inventory(context, tenantId), ordering.GetRequiredService<IOrderService>(),
            payments, Mock.Of<IInvoiceWriter>(MockBehavior.Strict), new DiscountService(context, tenant, Clock),
            new ZeroRateTaxCalculator(), tenant, new UnexpectedBoxCheckout(),
            new GuestOrderAccess(new EphemeralDataProtectionProvider()), new FulfilmentPromiseService(context, tenant, Clock),
            Mock.Of<IDeliveryCoverageService>(MockBehavior.Strict));
    }

    private async Task<(Guid CartId, string Token, Guid VariantId, string Version)> SeedCartAsync(Guid tenantId)
    {
        await using var context = Commerce(tenantId);
        var dish = new Product { TenantId = tenantId, Slug = "dish", Name = "Dish", Kind = ProductKinds.Simple, Status = ProductStatuses.Active };
        var variant = new ProductVariant { TenantId = tenantId, ProductId = dish.Id, Sku = "DISH", Name = "Regular" };
        var bundle = new Product { TenantId = tenantId, Slug = "box", Name = "Box", Kind = ProductKinds.Bundle, Status = ProductStatuses.Active };
        var slot = new BundleSlot { TenantId = tenantId, BundleProductId = bundle.Id, Name = "Dish", MinItems = 1, MaxItems = 1 };
        var token = CartAccess.MintToken();
        var cart = new Cart
        {
            TenantId = tenantId, Currency = "GBP", AnonymousToken = token,
            Items = [new CartItem
            {
                TenantId = tenantId, ProductVariantId = bundle.Id, IsBundle = true, BundleProductId = bundle.Id,
                Quantity = 1m, UnitPriceSnapshot = 20m, Sku = "BOX", NameSnapshot = "Box",
                Selections = [new CartItemSelection
                {
                    TenantId = tenantId, BundleSlotId = slot.Id, ProductVariantId = variant.Id,
                    Quantity = 1m, UnitPriceSnapshot = 20m, Sku = "DISH", NameSnapshot = "Dish",
                }],
            }],
        };
        context.Products.AddRange(dish, bundle);
        context.ProductVariants.Add(variant);
        context.BundleSlots.Add(slot);
        context.Carts.Add(cart);
        context.InventoryLevels.Add(new InventoryLevel { TenantId = tenantId, ProductVariantId = variant.Id, OnHand = 20m });
        context.FulfilmentCalendars.Add(new FulfilmentCalendar
        {
            TenantId = tenantId, Timezone = "Europe/London", IsActive = true, LeadDays = 7,
            CutoffLocalTime = new TimeOnly(23, 59), DeliveryDaysJson = "[\"friday\",\"saturday\"]",
        });
        await context.SaveChangesAsync();
        return (cart.Id, token, variant.Id, Convert.ToBase64String(cart.RowVersion));
    }

    private static CheckoutCommand Command(Guid cartId, string address, DateOnly date) => new(cartId, "Test", "Card",
        Delivery: new CheckoutDeliveryDetails(new CheckoutContactDto("buyer@example.com", "Test", "Buyer", "07700900123"),
            new DeliveryAddressDto(address, null, "London", null, "SW1A 1AA", "GB"), date));

    private static OrderDeliveryDetails Snapshot(Guid tenantId, Guid orderId, string address, DateOnly date) =>
        OrderDeliveryMapper.Create(tenantId, orderId, new OrderDeliveryDto(
            new CheckoutContactDto("buyer@example.com", "Test", "Buyer", "07700900123"),
            new DeliveryAddressDto(address, null, "London", null, "SW1A 1AA", "GB"), date,
            "Europe/London", new DeliveryRecipientDto("Test Buyer", "07700900123")));

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class UnexpectedBoxCheckout : IBoxCheckoutSupport
    {
        public Task<BoxCheckoutShape> PrepareForCheckoutAsync(Cart cart, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This SQL fixture uses a generic bundle cart, not a dedicated box session.");
    }

    private sealed class RecordingPayment : IPaymentInitiator
    {
        public Guid IntentId { get; } = Guid.NewGuid();
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("Test payment failure.");
            return Task.FromResult(new PaymentIntentRef(IntentId, "Pending", $"secret-{IntentId:N}"));
        }
    }

    private sealed class BeforeCheckoutSave : SaveChangesInterceptor
    {
        private int _paused;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid OrderId { get; private set; }
        public Exception? Failure { get; private set; }

        public async Task WaitUntilReachedAsync(Task checkout)
        {
            var completed = await Task.WhenAny(Reached.Task, checkout).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == checkout)
            {
                await checkout; // Surface the checkout failure instead of reporting a barrier timeout.
                throw new InvalidOperationException("Checkout completed before reaching its final save.");
            }
            await Reached.Task;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var charge = eventData.Context!.ChangeTracker.Entries<OrderChargeSummary>()
                .FirstOrDefault(entry => entry.State == EntityState.Added);
            if (charge is not null && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
                OrderId = charge.Entity.OrderId;
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Failure = eventData.Exception;
            return Task.CompletedTask;
        }

        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Failure = eventData.Exception;
            return ValueTask.FromResult(result);
        }
    }
}
