using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Application.Tests.Commerce;

using static Aonik.Application.Tests.Commerce.CartTestAccess;

/// <summary>
/// End-to-end checkout (Spec 042 §11/§12): reserve stock → create a ProductPurchase order via the
/// SharedKernel Ordering contract → record build-your-own-box contents → initiate funding → link it.
/// Uses the real Order spine (CoreOrderService) and test doubles for the Finance write contracts.
/// Each service call uses a fresh context over a shared in-memory store — the CoreOrderServiceTests
/// pattern that avoids EF InMemory change-tracking quirks and mirrors scoped contexts in production.
/// </summary>
public class CheckoutServiceTests
{
    private sealed class FakePaymentInitiator : TestPaymentState
    {
        public Guid LastOrderId { get; private set; }
        public decimal LastAmount { get; private set; }
        public string? LastProvider { get; private set; }
        public int FailTimes { get; set; }
        public override Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken ct = default)
        {
            if (FailTimes > 0)
            {
                FailTimes--;
                throw new InvalidOperationException("Simulated payment provider failure.");
            }
            LastOrderId = command.OrderId;
            LastAmount = command.Amount;
            LastProvider = command.Provider;
            return Task.FromResult(Record(command, "secret_123", "https://pay.example/checkout"));
        }
    }

    private sealed class FakeInvoiceWriter : IInvoiceWriter
    {
        public int Calls { get; private set; }
        public bool LoseCreateResponse { get; set; }
        public Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken ct = default)
        {
            Calls++;
            if (LoseCreateResponse) throw new InvalidOperationException("Invoice create response was lost.");
            return Task.FromResult(new InvoiceRef(Guid.NewGuid(), "INV-TEST", command.Lines.Sum(l => l.Quantity * l.UnitPrice), command.Currency));
        }
    }

    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"co_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"co_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();
        private readonly CommerceTestHarness.TestClock _clock = new();
        private readonly GuestOrderAccess _guestOrderAccess = new(new EphemeralDataProtectionProvider());

        public FakePaymentInitiator Payments { get; } = new();
        public FakeInvoiceWriter Invoices { get; } = new();

        public Harness() => _tenant = new TestTenantProvider(_tenantId);

        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb).Options, _tenantId);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options, _tenant, _user);

        public ProductService Products()
        {
            // ProductService and its Spec 066 option dependency must share one context.
            var ctx = Commerce();
            return new(ctx, _tenant, new ProductOptionService(ctx, _tenant, new ProductContentReviewFlagger(ctx), NullLogger<ProductOptionService>.Instance), NullLogger<ProductService>.Instance,
                CommerceTestHarness.NewContentService(ctx, _tenantId));
        }
        public ProductPricingService Pricing() => new(Commerce(), _tenant, _clock);
        public InventoryService Inventory() => new(Commerce(), _tenant, new Aonik.Infrastructure.Multitenancy.TenantContext { TenantId = _tenantId }, _clock);
        public CartService Carts() => new(Commerce(), _tenant, Pricing(), _clock);
        public DiscountService Discounts() => new(Commerce(), _tenant, _clock);
        public BoxCartService BoxCarts()
        {
            var ctx = Commerce();
            return new(ctx, _tenant, CommerceTestHarness.NewSelectionService(ctx, _tenantId), Inventory(),
                new NullTenantSettingStore(), new NullSettingProvider(), new GbpTenantCurrencyProvider(), new ProductPricingService(ctx, _tenant, _clock), _clock);
        }

        public CheckoutService Checkout() => new(
            Commerce(), Inventory(), new CoreOrderService(Ordering(), _tenant, _clock, _user),
            Payments, Invoices, Discounts(), new ZeroRateTaxCalculator(), _tenant, BoxCarts(), _guestOrderAccess,
            new FulfilmentPromiseService(Commerce(), _tenant, _clock), new ServedTestDeliveryCoverage(), CommerceTestHarness.Parties(), _clock);
    }

    [Fact]
    public async Task Checkout_Should_ReserveStock_CreateOrder_AndInitiateFunding()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Wellness Tea", ProductKinds.Variant,
            Variants: new[] { new CreateVariantLine("TEA-20", "20 bags") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));

        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        result.Total.Should().Be(5_000m);
        result.PaymentIntentId.Should().NotBeEmpty();
        h.Payments.LastOrderId.Should().Be(result.OrderId);
        h.Payments.LastAmount.Should().Be(5_000m);

        (await h.Inventory().GetAvailableAsync(variantId)).Should().Be(8m);

        await using var ordering = h.Ordering();
        var order = await ordering.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.OrderId);
        order.OrderType.Should().Be(OrderTypeCodes.ProductPurchase);
        order.AmountIn.Should().Be(5_000m);
        var line = order.Items.Single();
        line.Quantity.Should().Be(2m);
        line.UnitPrice.Should().Be(2_500m);
        line.ProductId.Should().Be(variantId);
        (await ordering.OrderFundingRefs.AnyAsync(f => f.OrderId == result.OrderId)).Should().BeTrue();

        (await h.Carts().GetCartAsync(cart.Id, Owner(cart)))!.OrderId.Should().Be(result.OrderId);
    }

    [Fact]
    public async Task Checkout_Should_FanOutBundleToComponents_AndRecordBoxContents()
    {
        var h = new Harness();
        var category = await h.Products().CreateCategoryAsync(new CreateCategoryCommand("granola", "Granola"));

        var variantIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var p = await h.Products().CreateProductAsync(new CreateProductCommand(
                $"granola-{i}", $"Granola {i}", ProductKinds.Simple, CategoryId: category.Id,
                Variants: new[] { new CreateVariantLine($"GRAN-{i}", $"Granola {i}") }));
            var v = p.Variants.Single().Id;
            await h.Pricing().SetPriceAsync(new SetPriceCommand(v, "GBP", 2_000m));
            await h.Inventory().SetOnHandAsync(v, 5m);
            variantIds.Add(v);
        }

        var box = await h.Products().CreateProductAsync(new CreateProductCommand(
            "wellness-box", "Build Your Own Box", ProductKinds.Bundle,
            BundlePricingMode: BundlePricingModes.Fixed, BundleFixedAmount: 12_000m, BundleCurrency: "GBP"));
        var slot = await h.Products().AddBundleSlotAsync(new AddBundleSlotCommand(
            box.Id, "Pick 6", MinItems: 6, MaxItems: 6, FromCategoryId: category.Id));

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        var selection = variantIds.Select(v => new BundleSelectionLine(slot.Id, v)).ToList();
        var cartDto = await h.Carts().AddBundleAsync(new AddBundleToCartCommand(cart.Id, box.Id, selection), Owner(cart));
        cartDto.Total.Should().Be(12_000m);

        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        result.Total.Should().Be(12_000m);

        await using var ordering = h.Ordering();
        var order = await ordering.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.OrderId);
        order.Items.Should().HaveCount(1);
        order.Items.Single().ProductId.Should().Be(box.Id);

        await using var commerce = h.Commerce();
        (await commerce.OrderBundleSelections.CountAsync(s => s.OrderId == result.OrderId)).Should().Be(6);

        foreach (var v in variantIds)
        {
            (await h.Inventory().GetAvailableAsync(v)).Should().Be(4m);
        }
    }

    [Fact]
    public async Task Checkout_Should_Throw_AndCreateNoOrder_WhenStockInsufficient()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "rare", "Rare Item", ProductKinds.Variant,
            Variants: new[] { new CreateVariantLine("RARE-1", "one") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 1_000m));
        await h.Inventory().SetOnHandAsync(variantId, 1m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));

        var act = async () => await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        await act.Should().ThrowAsync<InsufficientStockException>();

        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmPayment_Should_CommitInventory_CloseCart_AndCompleteOrder()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));
        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        await h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, result.Total, result.Currency);
        await h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, result.Total, result.Currency);

        (await h.Inventory().GetAvailableAsync(variantId)).Should().Be(8m);
        (await h.Carts().GetCartAsync(cart.Id, Owner(cart)))!.Status.Should().Be("CheckedOut");

        await using var ordering = h.Ordering();
        (await ordering.Orders.FirstAsync(o => o.Id == result.OrderId)).Status.Should().Be("Complete");
        (await ordering.Orders.CountAsync()).Should().Be(1);
        await using var commerce = h.Commerce();
        (await commerce.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Committed);
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(8m);
        stock.Reserved.Should().Be(0m);
    }

    [Fact]
    public async Task Checkout_Should_ApplyDiscount_AndFundTheDiscountedTotal()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);
        await h.Discounts().CreateAsync(new Aonik.Commerce.Services.Promotions.CreateDiscountCommand(
            "SAVE10", Aonik.Commerce.Entities.Promotions.DiscountKinds.Percentage, 10m));

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart)); // subtotal 5000

        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card", DiscountCode: "SAVE10"), Owner(cart));

        result.Subtotal.Should().Be(5_000m);
        result.DiscountTotal.Should().Be(500m);
        result.TaxTotal.Should().Be(0m);
        result.Total.Should().Be(4_500m);
        // Funding is for the discounted total, not the goods subtotal.
        h.Payments.LastAmount.Should().Be(4_500m);

        await using var commerce = h.Commerce();
        var charge = await commerce.OrderChargeSummaries.FirstAsync(c => c.OrderId == result.OrderId);
        charge.Total.Should().Be(4_500m);
        charge.DiscountCode.Should().Be("SAVE10");
    }

    [Theory]
    [InlineData("missing-intent")]
    [InlineData("wrong-intent")]
    [InlineData("missing-summary")]
    public async Task ConfirmPayment_Should_NotConvergeAnything_WithoutTheRecordedIntent(string missingEvidence)
    {
        var (h, checkout) = await PendingConfirmationAsync();
        if (missingEvidence == "missing-summary")
        {
            await using var setup = h.Commerce();
            setup.OrderChargeSummaries.Remove(await setup.OrderChargeSummaries.SingleAsync());
            await setup.SaveChangesAsync();
        }
        Guid? completedIntent = missingEvidence switch
        {
            "missing-intent" => null,
            "wrong-intent" => Guid.NewGuid(),
            _ => checkout.PaymentIntentId,
        };

        await h.Checkout().ConfirmPaymentAsync(checkout.OrderId, completedIntent ?? Guid.Empty, checkout.Total, checkout.Currency);

        await using var commerce = h.Commerce();
        (await commerce.Carts.SingleAsync()).Status.Should().Be(CartStatuses.Open);
        (await commerce.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Held);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(10m);
        stock.Reserved.Should().Be(2m);
        var summary = await commerce.OrderChargeSummaries.SingleOrDefaultAsync();
        if (missingEvidence == "missing-summary") summary.Should().BeNull();
        else summary!.PaymentStatus.Should().Be(checkout.PaymentStatus);
        await using var ordering = h.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().Be(OrderStatusCodes.Draft);
        (await commerce.Carts.SingleAsync()).OrderId.Should().Be(checkout.OrderId);
    }

    [Theory]
    [InlineData(OrderStatusCodes.Cancelled, false)]
    [InlineData(OrderStatusCodes.Cancelled, true)]
    [InlineData(OrderStatusCodes.Failed, true)]
    [InlineData(OrderStatusCodes.Expired, true)]
    [InlineData("Refunded", true)]
    public async Task ConfirmPayment_Should_NotReviveAnOrderThatMovedBeyondPaymentCompletion(string laterStatus, bool wasCompleted)
    {
        var (h, checkout) = await PendingConfirmationAsync();
        if (wasCompleted)
            await h.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId, checkout.Total, checkout.Currency);
        int historyCount;
        await using (var setup = h.Ordering())
        {
            (await setup.Orders.SingleAsync()).Status = laterStatus;
            await setup.SaveChangesAsync();
            historyCount = await setup.OrderHistoryEvents.CountAsync();
        }

        await h.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId, checkout.Total, checkout.Currency);

        await using var ordering = h.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().Be(laterStatus);
        (await ordering.OrderHistoryEvents.CountAsync()).Should().Be(historyCount);
        await using var commerce = h.Commerce();
        (await commerce.Carts.SingleAsync()).Status.Should().Be(wasCompleted ? CartStatuses.CheckedOut : CartStatuses.Open);
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be(
            wasCompleted ? CheckoutPaymentStatuses.Captured : checkout.PaymentStatus);
        (await commerce.InventoryReservations.SingleAsync()).Status.Should().Be(
            wasCompleted ? InventoryReservationStatuses.Committed : InventoryReservationStatuses.Held);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(wasCompleted ? 8m : 10m);
        stock.Reserved.Should().Be(wasCompleted ? 0m : 2m);
    }

    [Theory]
    [InlineData("Refunded")]
    [InlineData("PartiallyRefunded")]
    public async Task ConfirmPayment_Should_PreservePostCapturePaymentState_OnAnEmailRetry(string paymentStatus)
    {
        var (h, checkout) = await PendingConfirmationAsync();
        await h.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId, checkout.Total, checkout.Currency);
        await using (var setup = h.Commerce())
        {
            (await setup.OrderChargeSummaries.SingleAsync()).PaymentStatus = paymentStatus;
            await setup.SaveChangesAsync();
        }

        await h.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId, checkout.Total, checkout.Currency);

        await using var commerce = h.Commerce();
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be(paymentStatus);
        (await commerce.Carts.SingleAsync()).Status.Should().Be(CartStatuses.CheckedOut);
        (await commerce.InventoryLevels.SingleAsync()).OnHand.Should().Be(8m);
        await using var ordering = h.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().Be(OrderStatusCodes.Complete);
    }

    private static async Task<(Harness Harness, CheckoutResult Checkout)> PendingConfirmationAsync()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: [new CreateVariantLine("TEA-20", "20")]));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));
        var checkout = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        return (h, checkout);
    }

    [Fact]
    public async Task Checkout_Retry_Should_BeIdempotent_NotReReserveOrCreateSecondOrder()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));

        var first = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        var retry = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart)); // double-click

        // Same order + payment intent replayed; no second order; stock reserved exactly once.
        retry.OrderId.Should().Be(first.OrderId);
        retry.PaymentIntentId.Should().Be(first.PaymentIntentId);
        retry.Total.Should().Be(first.Total);
        // Provider launch handles survive the replay (persisted on the charge summary).
        retry.CheckoutUrl.Should().Be(first.CheckoutUrl).And.NotBeNull();
        retry.ClientSecret.Should().Be(first.ClientSecret).And.NotBeNull();
        (await h.Inventory().GetAvailableAsync(variantId)).Should().Be(8m);

        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Checkout_Abort_Then_Retry_Should_NotDuplicateReservations()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));

        // First attempt aborts at the payment step, after stock was reserved; cart stays Open.
        h.Payments.FailTimes = 1;
        var firstAttempt = async () => await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        // Retry succeeds and must not stack a second hold — release-before-reserve frees the orphan.
        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));
        result.Total.Should().Be(5_000m);
        (await h.Inventory().GetAvailableAsync(variantId)).Should().Be(8m); // 10 - 2, not 10 - 4
    }

    [Theory]
    [InlineData(OrderStatusCodes.Draft)]
    [InlineData("PendingFunding")]
    public async Task ConfirmPayment_Should_CompleteOrder_EvenWhenCartAlreadyCheckedOut(string orderStatus)
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 2m), Owner(cart));
        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        // Inventory, capture status and cart were saved before the order transition failed.
        await h.Inventory().CommitAsync(cart.Id);
        await using (var ctx = h.Commerce())
        {
            var c = await ctx.Carts.FirstAsync(x => x.Id == cart.Id);
            c.Status = CartStatuses.CheckedOut;
            (await ctx.OrderChargeSummaries.SingleAsync()).PaymentStatus = CheckoutPaymentStatuses.Captured;
            await ctx.SaveChangesAsync();
        }
        await using (var pending = h.Ordering())
        {
            (await pending.Orders.SingleAsync()).Status = orderStatus;
            await pending.SaveChangesAsync();
        }

        await h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, result.Total, result.Currency);

        await using var ordering = h.Ordering();
        (await ordering.Orders.FirstAsync(o => o.Id == result.OrderId)).Status.Should().Be("Complete");
        await using var commerce = h.Commerce();
        (await commerce.InventoryLevels.SingleAsync()).OnHand.Should().Be(8m);
        (await commerce.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Committed);
    }

    [Fact]
    public async Task AddItem_Should_RejectNonPositiveQuantity()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));

        var zero = async () => await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 0m), Owner(cart));
        var negative = async () => await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, -1m), Owner(cart));

        await zero.Should().ThrowAsync<ArgumentException>();
        await negative.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CartEdits_Should_BeRejected_AfterCheckoutStampsOrderId()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 1m), Owner(cart));
        await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        // Cart is still Open (payment pending) but OrderId is stamped — further edits must be rejected.
        var addAfter = async () => await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 1m), Owner(cart));
        await addAfter.Should().ThrowAsync<CartWriteConflictException>();
    }

    [Fact]
    public async Task Checkout_Should_RaiseInvoice_WhenCustomerAccountSupplied()
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Variant, Variants: new[] { new CreateVariantLine("TEA-20", "20") }));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 2_500m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId, 1m), Owner(cart));

        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card", CustomerAccountId: Guid.NewGuid()), Owner(cart));

        h.Invoices.Calls.Should().Be(1);
        result.InvoiceId.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recover_Should_RequireStaff_ForInvoiceCheckoutEvenBeforePreparationCommits(bool loseCreateResponse)
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "tea", "Tea", ProductKinds.Simple, Variants: [new CreateVariantLine("TEA", "Tea")]));
        var variantId = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variantId, "GBP", 25m));
        await h.Inventory().SetOnHandAsync(variantId, 10m);
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variantId), Owner(cart));
        h.Invoices.LoseCreateResponse = loseCreateResponse;
        var checkout = () => h.Checkout().CheckoutAsync(
            new CheckoutCommand(cart.Id, "Stripe", "Card", CustomerAccountId: Guid.NewGuid()), Owner(cart));
        if (loseCreateResponse) await checkout.Should().ThrowAsync<InvalidOperationException>();
        else await checkout();
        var state = await h.Checkout().GetPaymentStateAsync(cart.Id, Owner(cart));

        var recover = () => h.Checkout().RecoverAsync(cart.Id, state.PaymentIntentId!.Value,
            Owner(cart) with { ExpectedCartVersion = state.CartVersion });

        await recover.Should().ThrowAsync<StorefrontValidationException>().WithMessage("*staff assistance*");
        h.Invoices.Calls.Should().Be(1);
        await using var commerce = h.Commerce();
        var saved = await commerce.Carts.SingleAsync();
        saved.CheckoutState.Should().Be(loseCreateResponse ? CartCheckoutStates.Preparing : CartCheckoutStates.AwaitingPayment);
        if (loseCreateResponse)
        {
            saved.OrderId.Should().BeNull();
            (await commerce.OrderChargeSummaries.AnyAsync()).Should().BeFalse();
            h.Payments.LastOrderId.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.29)]
    [InlineData(0.295)]
    public async Task Checkout_Should_RejectBelowProviderMinimumOrFractionalPennies_BeforeClaiming(decimal price)
    {
        var h = new Harness();
        var product = await h.Products().CreateProductAsync(new CreateProductCommand(
            "small", "Small item", ProductKinds.Simple, Variants: [new CreateVariantLine("SMALL", "Small")]));
        var variant = product.Variants.Single().Id;
        await h.Pricing().SetPriceAsync(new SetPriceCommand(variant, "GBP", price));
        await h.Inventory().SetOnHandAsync(variant, 10m);
        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: Guid.NewGuid()));
        await h.Carts().AddItemAsync(new AddCartItemCommand(cart.Id, variant), Owner(cart));

        var checkout = () => h.Checkout().CheckoutAsync(new CheckoutCommand(cart.Id, "Stripe", "Card"), Owner(cart));

        await checkout.Should().ThrowAsync<StorefrontValidationException>().WithMessage("*from 0.30*");
        await using var commerce = h.Commerce();
        var saved = await commerce.Carts.SingleAsync();
        saved.CheckoutState.Should().BeNull();
        saved.CheckoutPreparationJson.Should().BeNull();
        saved.OrderId.Should().BeNull();
        (await commerce.InventoryReservations.CountAsync()).Should().Be(0);
        h.Payments.LastOrderId.Should().BeEmpty();
        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(0);
    }
}
