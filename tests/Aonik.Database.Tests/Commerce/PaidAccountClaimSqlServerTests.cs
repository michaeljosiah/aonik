using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Ordering;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests.Commerce;

public class PaidAccountClaimSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompetingVerifiedClaims_Should_ConvergeOnlyForTheSameOwner_WithoutChangingFinancialHistory(bool sameOwner)
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        await using var ordering = OrderingProvider(tenantId);
        // The fixture's EnsureCreated context has an intentionally unscoped cached model.
        // Runtime canonical reads use a separate EF provider and the actual tenant context.
        await using var canonicalProvider = new ServiceCollection().AddEntityFrameworkSqlServer().BuildServiceProvider();
        var canonicalOptions = new DbContextOptionsBuilder<AonikDbContext>(database.CreateOptions<AonikDbContext>())
            .UseInternalServiceProvider(canonicalProvider).Options;
        var seed = await SeedPaidAsync(tenantId, ordering, canonicalOptions);
        var before = await ReadFinancialStateAsync(tenantId, canonicalOptions);
        var ownerA = Guid.NewGuid();
        var ownerB = sameOwner ? ownerA : Guid.NewGuid();
        var gateA = new BeforeOwnershipSave();
        var gateB = new BeforeOwnershipSave();
        await using var contextA = Commerce(tenantId, gateA);
        await using var contextB = Commerce(tenantId, gateB);
        contextA.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        await using var scopeA = ordering.CreateAsyncScope();
        await using var scopeB = ordering.CreateAsyncScope();
        var issuer = new Mock<IPaidAccountAccessService>(MockBehavior.Strict);
        var first = Handler(contextA, scopeA.ServiceProvider, tenantId, issuer.Object)
            .HandleAsync(Verified(seed, tenantId, ownerA));
        try
        {
            await gateA.WaitUntilReachedAsync(first);
            var second = Handler(contextB, scopeB.ServiceProvider, tenantId, issuer.Object)
                .HandleAsync(Verified(seed, tenantId, ownerB));
            await gateB.WaitUntilReachedAsync(second);
            gateA.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(30));
            gateB.Release.TrySetResult();
            if (sameOwner) await second.WaitAsync(TimeSpan.FromSeconds(30));
            else
            {
                var losingClaim = () => second.WaitAsync(TimeSpan.FromSeconds(30));
                await losingClaim.Should().ThrowAsync<InvalidOperationException>().WithMessage("*another account*");
            }

            gateB.Failure.Should().BeOfType<DbUpdateConcurrencyException>("both claims read the unowned native version before either save");
            contextB.ChangeTracker.HasChanges().Should().BeFalse();
            await contextB.SaveChangesAsync();
            await Handler(contextA, scopeA.ServiceProvider, tenantId, issuer.Object)
                .HandleAsync(Verified(seed, tenantId, ownerA));
            await using var verify = Commerce(tenantId);
            var cart = await verify.Carts.AsNoTracking().SingleAsync();
            cart.BuyerPartyId.Should().Be(ownerA);
            cart.RowVersion.Should().HaveCount(8).And.NotEqual(seed.CartVersion);
            cart.AnonymousToken.Should().Be(seed.Token);
            cart.CheckoutPreparationJson.Should().Be(seed.Preparation);
            cart.OrderId.Should().Be(seed.OrderId);
            cart.Status.Should().Be(CartStatuses.CheckedOut);
            cart.LastActivityAtUtc.Should().Be(Clock.UtcNow);
            (await verify.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(seed.PaymentIntentId);
            (await ReadFinancialStateAsync(tenantId, canonicalOptions)).Should().Be(before,
                "claiming ownership neither changes the historical payer/payment/funding rows nor creates duplicate rows or outbox events");
            issuer.VerifyNoOtherCalls();
        }
        finally
        {
            gateA.Release.TrySetResult();
            gateB.Release.TrySetResult();
        }
    }

    private CommerceDbContext Commerce(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
    }

    private static CommerceAccountAccessVerifiedHandler Handler(CommerceDbContext context, IServiceProvider ordering,
        Guid tenantId, IPaidAccountAccessService issuer) => new(new PaidCheckoutAccountAccessService(context,
            new TestTenantProvider(tenantId), ordering.GetRequiredService<IOrderService>(), issuer));

    private ServiceProvider OrderingProvider(Guid tenantId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider());
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IOrderNumberGenerator, Aonik.TestSupport.Ordering.TestOrderNumberGenerator>();
        services.AddOrderingModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString
        }).Build());
        return services.BuildServiceProvider();
    }

    private static AccountAccessVerifiedEvent Verified(Seed seed, Guid tenantId, Guid owner)
        => new(tenantId, Guid.NewGuid(), seed.CartId, seed.OrderId, seed.PaymentIntentId, seed.GuestPartyId, owner);

    private async Task<Seed> SeedPaidAsync(Guid tenantId, ServiceProvider ordering,
        DbContextOptions<AonikDbContext> canonicalOptions)
    {
        var guestId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        await using var scope = ordering.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var order = await orders.CreateAsync(new CreateOrderCommand(OrderTypeCodes.ProductPurchase, guestId, "GBP",
            [new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 95m, "GBP", Quantity: 1m, UnitPrice: 95m, Sku: "BOX")],
            IdempotencyKey: $"paid-account-claim:{Guid.NewGuid():N}"));
        await orders.LinkFundingAsync(order.Id, intentId);
        await orders.TransitionAsync(order.Id, OrderStatusCodes.Complete, "Paid test fixture", expectedFromStatus: order.Status);
        await using (var finance = new AonikDbContext(canonicalOptions, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock))
        {
            finance.Set<PaymentIntent>().Add(new PaymentIntent
            {
                Id = intentId, TenantId = tenantId, OrderId = order.Id, PayerPartyId = guestId,
                Amount = 95, Currency = "GBP", Status = "Captured", ProviderCode = "Stripe", ProviderReference = "cs_claim_test"
            });
            finance.Payments.Add(new Payment
            {
                TenantId = tenantId, PaymentIntentId = intentId, Amount = 95, Currency = "GBP", Provider = "Stripe",
                ProviderReference = "pi_claim_test", CapturedAt = Clock.UtcNow, OutcomeStatus = "Captured", OutcomeJson = "{}"
            });
            await finance.SaveChangesAsync();
        }
        var delivery = new OrderDeliveryDto(new("paid@example.test", "Paid", "Guest", "07700900123"),
            new("1 Test Road", null, "London", null, "SW1A 1AA", "GB"), new DateOnly(2026, 10, 16),
            "Europe/London", new("Paid Guest", "07700900123"));
        var preparation = new CheckoutPreparation(intentId, guestId, "GBP", "Stripe", "Card", null, null, null,
            95, 0, null, null, 0, 95, [], [], [], [], delivery, CreateAccount: true).Serialize();
        await using var commerce = Commerce(tenantId);
        var cart = new Cart
        {
            TenantId = tenantId, OrderId = order.Id, Status = CartStatuses.CheckedOut, Currency = "GBP",
            CheckoutPreparationJson = preparation, AnonymousToken = CartAccess.MintToken(), LastActivityAtUtc = Clock.UtcNow
        };
        commerce.Carts.Add(cart);
        commerce.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = tenantId, OrderId = order.Id, PaymentIntentId = intentId, Currency = "GBP",
            Subtotal = 95, Total = 95, PaymentStatus = CheckoutPaymentStatuses.Captured
        });
        await commerce.SaveChangesAsync();
        cart.RowVersion.Should().HaveCount(8);
        return new(cart.Id, order.Id, intentId, guestId, cart.AnonymousToken!, preparation, cart.RowVersion);
    }

    private static async Task<string> ReadFinancialStateAsync(Guid tenantId, DbContextOptions<AonikDbContext> options)
    {
        await using var context = new AonikDbContext(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
        var order = await context.Set<Order>().AsNoTracking().SingleAsync(row => row.TenantId == tenantId);
        var intent = await context.Set<PaymentIntent>().AsNoTracking().SingleAsync(row => row.TenantId == tenantId);
        var payment = await context.Payments.AsNoTracking().SingleAsync(row => row.TenantId == tenantId);
        var funding = await context.OrderFundingRefs.AsNoTracking().SingleAsync(row => row.TenantId == tenantId);
        intent.RowVersion.Should().HaveCount(8);
        payment.RowVersion.Should().HaveCount(8);
        funding.RowVersion.Should().HaveCount(8);
        return JsonSerializer.Serialize(new
        {
            Order = new { order.Id, order.PayerPartyId, order.Status, order.RowVersion },
            Intent = new { intent.Id, intent.OrderId, intent.PayerPartyId, intent.Status, intent.Amount, intent.Currency, intent.RowVersion },
            Payment = new { payment.Id, payment.PaymentIntentId, payment.Amount, payment.Currency, payment.RowVersion },
            Funding = new { funding.Id, funding.OrderId, funding.PaymentIntentId, funding.RowVersion },
            OutboxCount = await context.OutboxMessages.CountAsync(row => row.TenantId == tenantId)
        });
    }

    private sealed record Seed(Guid CartId, Guid OrderId, Guid PaymentIntentId, Guid GuestPartyId,
        string Token, string Preparation, byte[] CartVersion);

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class BeforeOwnershipSave : SaveChangesInterceptor
    {
        private int _paused;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; private set; }

        public async Task WaitUntilReachedAsync(Task claim)
        {
            var completed = await Task.WhenAny(Reached.Task, claim).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == claim)
            {
                await claim;
                throw new InvalidOperationException("Claim completed before its ownership save.");
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var ownershipWrite = eventData.Context!.ChangeTracker.Entries<Cart>().Any(entry =>
                entry.State == EntityState.Modified && entry.Property(cart => cart.BuyerPartyId).IsModified);
            if (ownershipWrite && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }

        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Failure = eventData.Exception;
            return ValueTask.FromResult(result);
        }
    }
}
