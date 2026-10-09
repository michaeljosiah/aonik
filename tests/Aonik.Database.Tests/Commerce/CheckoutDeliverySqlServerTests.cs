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
using Aonik.SharedKernel.Persistence;
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

    [SkippableFact]
    public async Task CompetingCheckouts_Should_ClaimOneAttemptBeforeMoney_AndReplayItsOriginalSnapshot()
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
        var payments = new RecordingPayment();
        var access = CartAccessContext.ForGuest(seeded.Token, seeded.Version);
        var first = Checkout(contextA, scopeA.ServiceProvider, tenantId, payments)
            .CheckoutAsync(Command(seeded.CartId, "1 Original Road", FirstDate), access);
        try
        {
            await gateA.WaitUntilReachedAsync(first);
            var second = Checkout(contextB, scopeB.ServiceProvider, tenantId, payments)
                .CheckoutAsync(Command(seeded.CartId, "2 Losing Road", FirstDate.AddDays(1)), access);
            await gateB.WaitUntilReachedAsync(second);
            payments.Calls.Should().Be(0);
            gateA.Release.TrySetResult();
            var winner = await first.WaitAsync(TimeSpan.FromSeconds(30));
            gateB.Release.TrySetResult();
            var replay = await second.WaitAsync(TimeSpan.FromSeconds(30));
            gateB.Failure.Should().BeOfType<DbUpdateConcurrencyException>();
            (replay with { GuestOrderToken = null }).Should().Be(winner with { GuestOrderToken = null });
            payments.Calls.Should().Be(1);
            await contextB.SaveChangesAsync();
            await using var verify = Commerce(tenantId);
            var delivery = await verify.OrderDeliveryDetails.SingleAsync();
            delivery.AddressLine1.Should().Be("1 Original Road");
            delivery.DeliveryDate.Should().Be(FirstDate);
            delivery.RowVersion.Should().HaveCount(8);
            (await verify.Carts.SingleAsync()).RowVersion.Should().HaveCount(8);
            (await verify.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(winner.PaymentIntentId);
            (await verify.OrderBundleSelections.CountAsync()).Should().Be(1);
            (await verify.InventoryReservations.CountAsync(r => r.Status == InventoryReservationStatuses.Held)).Should().Be(1);
            (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(1m);
            await using var canonicalServices = new ServiceCollection().AddEntityFrameworkSqlServer().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<AonikDbContext>(database.CreateOptions<AonikDbContext>())
                .UseInternalServiceProvider(canonicalServices).Options;
            await using var funding = new AonikDbContext(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
            var reference = await funding.OrderFundingRefs.SingleAsync(r => r.OrderId == winner.OrderId);
            reference.PaymentIntentId.Should().Be(winner.PaymentIntentId);
            reference.RowVersion.Should().HaveCount(8);
            await using var foreign = new AonikDbContext(options, new TestTenantProvider(Guid.NewGuid()), new TestCurrentUserProvider(), Clock);
            (await foreign.OrderFundingRefs.AnyAsync(r => r.OrderId == winner.OrderId)).Should().BeFalse();
        }
        finally { gateA.Release.TrySetResult(); gateB.Release.TrySetResult(); }
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewerCartWriteBeforeClaim_Should_WinWithoutLeakingOrCreatingPayment(bool adopt)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        var gate = new BeforeCheckoutSave();
        await using var context = Commerce(tenantId, gate);
        var payment = new RecordingPayment();
        var access = CartAccessContext.ForGuest(seeded.Token, seeded.Version);
        var pending = Checkout(context, scope.ServiceProvider, tenantId, payment)
            .CheckoutAsync(Command(seeded.CartId, "Original address", FirstDate), access);
        try
        {
            await gate.WaitUntilReachedAsync(pending);
            await using (var edit = Commerce(tenantId))
            {
                var tenant = new TestTenantProvider(tenantId);
                var carts = new CartService(edit, tenant, new ProductPricingService(edit, tenant, Clock), Clock);
                if (adopt) await carts.AdoptAsync(seeded.CartId, Guid.NewGuid(), access);
                else await carts.SaveCheckoutDraftAsync(seeded.CartId, new CartCheckoutDraftDto(Notes: "Newer draft"), access);
            }
            gate.Release.TrySetResult();
            var act = async () => await pending;
            if (adopt) await act.Should().ThrowAsync<NotFoundException>();
            else await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
            await Inventory(context, tenantId).SetOnHandAsync(seeded.VariantId, 25m);
            await using var verify = Commerce(tenantId);
            (await verify.OrderChargeSummaries.CountAsync()).Should().Be(0);
            (await verify.OrderDeliveryDetails.CountAsync()).Should().Be(0);
            (await verify.InventoryReservations.CountAsync()).Should().Be(0);
            payment.Calls.Should().Be(0);
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
    public async Task FailedPaymentInitiation_Should_KeepOneDurableSnapshotAndHold_ForSameAttemptResume()
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
            (await verify.OrderBundleSelections.CountAsync()).Should().Be(1);
            (await verify.OrderChargeSummaries.CountAsync()).Should().Be(1);
            (await verify.OrderDeliveryDetails.CountAsync()).Should().Be(1);
            (await verify.Carts.SingleAsync()).CheckoutState.Should().Be(CartCheckoutStates.AwaitingPayment);
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

    [SkippableFact]
    public async Task OwnedGenericRetry_Should_RemoveAndRestoreOptionalDelivery_OnTheSameUniqueSnapshot()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        string version;
        await using (var setup = Commerce(tenantId))
        {
            var cart = await setup.Carts.SingleAsync();
            cart.BuyerPartyId = partyId;
            cart.AnonymousToken = null;
            await setup.SaveChangesAsync();
            version = Convert.ToBase64String(cart.RowVersion);
        }
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        await using var context = Commerce(tenantId);
        var checkout = Checkout(context, scope.ServiceProvider, tenantId, new RecordingPayment());
        var access = CartAccessContext.ForParty(partyId, version);
        var first = await checkout.CheckoutAsync(Command(seeded.CartId, "Original address", FirstDate), access);
        Guid snapshotId;
        await using (var read = Commerce(tenantId)) snapshotId = (await read.OrderDeliveryDetails.SingleAsync()).Id;
        var state = await checkout.GetPaymentStateAsync(seeded.CartId, access);
        var reopened = await checkout.RecoverAsync(seeded.CartId, first.PaymentIntentId, access with { ExpectedCartVersion = state.CartVersion });
        var second = await checkout.CheckoutAsync(Command(seeded.CartId, "Unused", FirstDate) with { Delivery = null },
            access with { ExpectedCartVersion = reopened.CartVersion });
        second.OrderId.Should().Be(first.OrderId);
        await using (var read = Commerce(tenantId)) (await read.OrderDeliveryDetails.CountAsync()).Should().Be(0);
        state = await checkout.GetPaymentStateAsync(seeded.CartId, access);
        reopened = await checkout.RecoverAsync(seeded.CartId, second.PaymentIntentId, access with { ExpectedCartVersion = state.CartVersion });
        var third = await checkout.CheckoutAsync(Command(seeded.CartId, "Replacement address", FirstDate.AddDays(1)),
            access with { ExpectedCartVersion = reopened.CartVersion });
        third.OrderId.Should().Be(first.OrderId);
        await using var verify = Commerce(tenantId);
        var snapshot = await verify.OrderDeliveryDetails.SingleAsync();
        snapshot.Id.Should().Be(snapshotId);
        snapshot.AddressLine1.Should().Be("Replacement address");
        (await verify.OrderDeliveryDetails.IncludeSoftDeleted().CountAsync(row => row.TenantId == tenantId)).Should().Be(1);
        (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(1m);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyConfirmationRollback_Should_NotReflushInventoryOrDiscount_OnTheSameContext(bool failDuringDiscount)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedCartAsync(tenantId);
        await using (var setup = Commerce(tenantId))
        {
            setup.Discounts.Add(new Discount { TenantId = tenantId, Code = "SAVE10", Value = 10m });
            await setup.SaveChangesAsync();
        }
        await using var provider = OrderingProvider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        var failure = new FailConfirmationSave(failDuringDiscount);
        await using var context = Commerce(tenantId, failure);
        var checkout = Checkout(context, scope.ServiceProvider, tenantId, new RecordingPayment());
        var pending = await checkout.CheckoutAsync(Command(seeded.CartId, "Original address", FirstDate) with { DiscountCode = "SAVE10" },
            CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        await using (var legacy = Commerce(tenantId))
        {
            var cart = await legacy.Carts.SingleAsync();
            cart.CheckoutPreparationJson = null;
            cart.CheckoutState = null;
            await legacy.SaveChangesAsync();
        }
        failure.Armed = true;

        var confirm = () => checkout.ConfirmPaymentAsync(pending.OrderId, pending.PaymentIntentId, pending.Total, pending.Currency);

        await confirm.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test confirmation save failure.");
        failure.WasTriggered.Should().BeTrue();
        // This save must reload the rolled-back inventory version and must not flush either
        // the rejected stock commit or the discount increment left by a later failure.
        await Inventory(context, tenantId).SetOnHandAsync(seeded.VariantId, 25m);
        await using (var verify = Commerce(tenantId))
        {
            (await verify.Carts.SingleAsync()).Status.Should().Be(CartStatuses.Open);
            (await verify.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be("Pending");
            (await verify.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Held);
            (await verify.Discounts.SingleAsync()).TimesRedeemed.Should().Be(0);
            var stock = await verify.InventoryLevels.SingleAsync();
            stock.OnHand.Should().Be(25m);
            stock.Reserved.Should().Be(1m);
        }
        (await checkout.ConfirmPaymentAsync(pending.OrderId, pending.PaymentIntentId, pending.Total, pending.Currency)).Should().BeTrue();
        await using var completed = Commerce(tenantId);
        (await completed.Discounts.SingleAsync()).TimesRedeemed.Should().Be(1);
        (await completed.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Committed);
        (await completed.InventoryLevels.SingleAsync()).OnHand.Should().Be(24m);
        (await completed.InventoryLevels.SingleAsync()).Reserved.Should().Be(0m);
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
        var parties = new Mock<IPartyService>(MockBehavior.Strict);
        parties.Setup(service => service.EnsureUnverifiedGuestPartyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CreatePartyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CreatePartyRequest details, CancellationToken _) =>
                new PartyResponse(id, details.DisplayName, details.PartyType, "Unverified"));
        return new CheckoutService(context, Inventory(context, tenantId), ordering.GetRequiredService<IOrderService>(),
            payments, Mock.Of<IInvoiceWriter>(MockBehavior.Strict), new DiscountService(context, tenant, Clock),
            new ZeroRateTaxCalculator(), tenant, new UnexpectedBoxCheckout(),
            new GuestOrderAccess(new EphemeralDataProtectionProvider()), new FulfilmentPromiseService(context, tenant, Clock),
            Mock.Of<IDeliveryCoverageService>(MockBehavior.Strict), parties.Object, Clock);
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

    private static CheckoutCommand Command(Guid cartId, string address, DateOnly date) => new(cartId, "Stripe", "Card",
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
        private readonly Dictionary<Guid, PaymentIntentStateRef> _states = [];
        public Guid IntentId { get; private set; }
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("Test payment failure.");
            IntentId = command.PaymentIntentId!.Value;
            _states[IntentId] = new(IntentId, command.OrderId, command.Amount, command.Currency, "Pending", false, "https://pay.example/session");
            return Task.FromResult(new PaymentIntentRef(IntentId, "Pending", $"secret-{IntentId:N}", "https://pay.example/session"));
        }
        public Task<PaymentIntentStateRef?> GetStateAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
            => Task.FromResult(_states.GetValueOrDefault(paymentIntentId));
        public Task<PaymentIntentStateRef> ExpireAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
        {
            var state = _states[paymentIntentId] with { Status = "Cancelled", CanNoLongerPay = true, CheckoutUrl = null };
            _states[paymentIntentId] = state;
            return Task.FromResult(state);
        }
    }

    private sealed class BeforeCheckoutSave : SaveChangesInterceptor
    {
        private int _paused;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; private set; }

        public async Task WaitUntilReachedAsync(Task checkout)
        {
            var completed = await Task.WhenAny(Reached.Task, checkout).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == checkout)
            {
                await checkout; // Surface the checkout failure instead of reporting a barrier timeout.
                throw new InvalidOperationException("Checkout completed before reaching its initial claim.");
            }
            await Reached.Task;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var cart = eventData.Context!.ChangeTracker.Entries<Cart>()
                .FirstOrDefault(entry => entry.State == EntityState.Modified && entry.Entity.CheckoutState == CartCheckoutStates.Preparing);
            if (cart is not null && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
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

    private sealed class FailConfirmationSave(bool failDuringDiscount) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool WasTriggered { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            var targetWrite = failDuringDiscount
                ? context.ChangeTracker.Entries<Discount>().Any(e => e.State == EntityState.Modified && e.Entity.TimesRedeemed == 1)
                : context.ChangeTracker.Entries<InventoryReservation>().Any(e => e.State == EntityState.Modified
                    && e.Entity.Status == InventoryReservationStatuses.Committed);
            if (Armed && !WasTriggered && targetWrite)
            {
                WasTriggered = true;
                throw new InvalidOperationException("Test confirmation save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
