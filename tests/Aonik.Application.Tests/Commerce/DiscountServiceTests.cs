using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.TestSupport.Multitenancy;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class DiscountServiceTests
{
    private static readonly Guid Goods = Guid.NewGuid();
    private static DiscountChargeLine[] Lines(decimal amount) => [new(0, Goods, amount)];

    [Fact]
    public async Task Percentage_Should_NormalizeCodeAndAllocateWithoutChangingQuotedGoods()
    {
        using var test = new Harness();
        await test.Service.CreateAsync(new(" save10 ", DiscountKinds.Percentage, 10m));
        var result = await test.Service.ComputeAsync(" SAVE10  ", Lines(5000m), "NGN");
        result.Amount.Should().Be(500m);
        result.Code.Should().Be("SAVE10");
        result.Allocations.Should().Equal(new DiscountAllocation(0, 500m));
    }

    [Fact]
    public async Task FixedAmount_Should_CapAtEligibleSubtotalAndRejectOtherCurrency()
    {
        using var test = new Harness();
        await test.Service.CreateAsync(new("BIG", DiscountKinds.FixedAmount, 9999m, "NGN"));
        (await test.Service.ComputeAsync("BIG", Lines(5000m), "NGN")).Amount.Should().Be(5000m);
        var wrongCurrency = () => test.Service.ComputeAsync("BIG", Lines(5000m), "USD");
        (await wrongCurrency.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.CurrencyMismatch);
    }

    [Fact]
    public async Task Compute_Should_ReturnNoDiscountForBlankCode()
    {
        using var test = new Harness();
        var result = await test.Service.ComputeAsync(" ", Lines(10), "GBP");
        result.Should().BeEquivalentTo(new DiscountComputation(null, null, 0m, []));
    }

    [Theory]
    [InlineData("unknown", DiscountException.Invalid)]
    [InlineData("expired", DiscountException.Expired)]
    [InlineData("inactive", DiscountException.Inactive)]
    [InlineData("exhausted", DiscountException.AlreadyUsed)]
    public async Task Compute_Should_ExplainOrdinaryApplicabilityFailure(string scenario, string expectedCode)
    {
        using var test = new Harness();
        var discount = new Discount { TenantId = test.TenantId, Code = "TEST", Value = 10m };
        if (scenario == "expired") discount.ExpiresAt = test.Clock.UtcNow;
        if (scenario == "inactive") discount.IsActive = false;
        if (scenario == "exhausted") discount.MaxRedemptions = 0;
        if (scenario != "unknown") { test.Db.Discounts.Add(discount); await test.Db.SaveChangesAsync(); }
        var act = () => test.Service.ComputeAsync("TEST", Lines(10m), "GBP");
        (await act.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("Percentage", 0)]
    [InlineData("Percentage", 101)]
    [InlineData("FixedAmount", -1)]
    [InlineData("FixedAmount", 1.001)]
    [InlineData("unknown", 10)]
    public async Task Definition_Should_RejectInvalidNewAndLegacyAmounts(string kind, decimal value)
    {
        using var test = new Harness();
        var create = () => test.Service.CreateAsync(new("BAD", kind, value, "GBP"));
        await create.Should().ThrowAsync<InvalidStateException>();
        test.Db.Discounts.Add(new Discount { TenantId = test.TenantId, Code = "BAD", Kind = kind, Value = value, Currency = "GBP" });
        await test.Db.SaveChangesAsync();
        var read = () => test.Service.ComputeAsync("BAD", Lines(10), "GBP");
        (await read.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.Invalid);
    }

    [Fact]
    public async Task Eligibility_Should_UseCatalogProductsAndExcludeStoredValueDeliveryAndUnknownKinds()
    {
        using var test = new Harness();
        var selected = await test.ProductAsync("Selected");
        var other = await test.ProductAsync("Other");
        await test.Service.CreateAsync(new("SELECTED", DiscountKinds.Percentage, 50, EligibleProductIds: [selected]));
        DiscountChargeLine[] lines = [new(7, selected, 20), new(3, other, 20), new(8, selected, 100, "GiftCardValue"),
            new(9, selected, 5, "Delivery"), new(10, selected, 50, "FutureCharge")];
        var result = await test.Service.ComputeAsync("SELECTED", lines, "GBP");
        result.Amount.Should().Be(10);
        result.Allocations.Should().Equal(new DiscountAllocation(7, 10));
        var ineligible = () => test.Service.ComputeAsync("SELECTED", [new(0, other, 10)], "GBP");
        (await ineligible.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.NotEligible);
    }

    [Fact]
    public async Task Compute_Should_FailClosedOnNormalizedLegacyCollisionAndTenantMismatch()
    {
        using var test = new Harness();
        test.Db.Discounts.AddRange(new Discount { TenantId = test.TenantId, Code = " Save ", Value = 10 },
            new Discount { TenantId = test.TenantId, Code = "SAVE", Value = 10 },
            new Discount { TenantId = Guid.NewGuid(), Code = "FOREIGN", Value = 10 });
        await test.Db.SaveChangesAsync();
        foreach (var code in new[] { "save", "FOREIGN" })
        {
            var act = () => test.Service.ComputeAsync(code, Lines(10), "GBP");
            (await act.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.Invalid);
        }
    }

    [Fact]
    public async Task Reserve_Should_RejectChangedAllocationEvenWhenAmountIsUnchanged()
    {
        using var test = new Harness();
        var first = await test.ProductAsync("First");
        var second = await test.ProductAsync("Second");
        var discount = await test.Service.CreateAsync(new("SAME", DiscountKinds.Percentage, 10, EligibleProductIds: [first]));
        DiscountChargeLine[] lines = [new(0, first, 20), new(1, second, 20)];
        var quote = await test.Service.ComputeAsync("SAME", lines, "GBP");
        await test.Service.UpdateAsync(discount.Id, new("Percentage", 10, true, null, null, null, [second], discount.Version));
        var cartId = Guid.NewGuid();
        var act = () => test.Service.ReserveTrackedAsync(cartId, Guid.NewGuid(), "SAME", lines, "GBP", quote);
        (await act.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.PriceChanged);
        await test.Db.SaveChangesAsync();
        (await test.Db.DiscountReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reservation_Should_ConsumeExactlyOnceDespiteLaterCampaignChanges()
    {
        using var test = new Harness();
        var discount = await test.Service.CreateAsync(new("ONCE", DiscountKinds.Percentage, 10, MaxRedemptions: 1));
        var quote = await test.Service.ComputeAsync("ONCE", Lines(10), "GBP");
        var cart = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var reservation = (await test.Service.ReserveTrackedAsync(cart, attempt, "ONCE", Lines(10), "GBP", quote))!.Value;
        await test.Db.SaveChangesAsync();
        (await test.Service.ReserveTrackedAsync(cart, attempt, "ONCE", Lines(10), "GBP", quote)).Should().Be(reservation);
        var exhausted = () => test.Service.ComputeAsync("ONCE", Lines(10), "GBP");
        (await exhausted.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.AlreadyUsed);
        await test.Service.UpdateAsync(discount.Id, new("Percentage", 20, false, null, 1, test.Clock.UtcNow.AddMinutes(-1), null, ""));
        await test.Service.CommitTrackedAsync(cart, reservation, attempt, discount.Id);
        await test.Db.SaveChangesAsync();
        await test.Service.CommitTrackedAsync(cart, reservation, attempt, discount.Id);
        await test.Db.SaveChangesAsync();
        (await test.Db.Discounts.AsNoTracking().SingleAsync()).TimesRedeemed.Should().Be(1);
        (await test.Db.DiscountReservations.AsNoTracking().SingleAsync()).Status.Should().Be(DiscountReservationStatuses.Redeemed);
        var release = () => test.Service.ReleaseTrackedAsync(cart, reservation, attempt);
        (await release.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.Conflict);
    }

    [Fact]
    public async Task Release_Should_RequireExactAttemptAndPermitOnlyNewAttemptReuse()
    {
        using var test = new Harness();
        await test.Service.CreateAsync(new("ONCE", DiscountKinds.Percentage, 10, MaxRedemptions: 1));
        var quote = await test.Service.ComputeAsync("ONCE", Lines(10), "GBP");
        var cart = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var reservation = (await test.Service.ReserveTrackedAsync(cart, attempt, "ONCE", Lines(10), "GBP", quote))!.Value;
        await test.Db.SaveChangesAsync();
        var wrong = () => test.Service.ReleaseTrackedAsync(cart, reservation, Guid.NewGuid());
        await wrong.Should().ThrowAsync<DiscountException>();
        await test.Service.ReleaseTrackedAsync(cart, reservation, attempt);
        await test.Db.SaveChangesAsync();
        var oldAttempt = () => test.Service.ReserveTrackedAsync(cart, attempt, "ONCE", Lines(10), "GBP", quote);
        await oldAttempt.Should().ThrowAsync<DiscountException>();
        (await test.Service.ReserveTrackedAsync(cart, Guid.NewGuid(), "ONCE", Lines(10), "GBP", quote)).Should().Be(reservation);
        await test.Db.SaveChangesAsync();
        (await test.Db.DiscountReservations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Admin_Should_ShowReservationsAndPreventReducingLimitBelowCommittedUsage()
    {
        using var test = new Harness();
        var discount = await test.Service.CreateAsync(new("ONCE", DiscountKinds.Percentage, 10, MaxRedemptions: 2));
        var quote = await test.Service.ComputeAsync("ONCE", Lines(10), "GBP");
        await test.Service.ReserveTrackedAsync(Guid.NewGuid(), Guid.NewGuid(), "ONCE", Lines(10), "GBP", quote);
        await test.Db.SaveChangesAsync();
        var page = await test.Service.ListAsync(new(Search: " once ", IsActive: true));
        page.Items.Should().ContainSingle().Which.ReservedCount.Should().Be(1);
        var reduce = () => test.Service.UpdateAsync(discount.Id, new("Percentage", 10, true, null, 0, null, null, ""));
        await reduce.Should().ThrowAsync<InvalidStateException>();
        var invalidVersion = () => test.Service.UpdateAsync(discount.Id, new("Percentage", 10, true, null, 2, null, null, "bad-base64"));
        (await invalidVersion.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.Conflict);
    }

    [Fact]
    public async Task Admin_Should_ValidateNewEligibilityButPreserveExistingRetiredSelection()
    {
        using var test = new Harness();
        var product = await test.ProductAsync("Retired");
        var discount = await test.Service.CreateAsync(new("SAVE", DiscountKinds.Percentage, 10, EligibleProductIds: [product]));
        (await test.Db.Products.SingleAsync()).IsDeleted = true;
        await test.Db.SaveChangesAsync();
        var updated = await test.Service.UpdateAsync(discount.Id, new("Percentage", 10, false, null, null, null, [product], ""));
        updated.EligibleProductIds.Should().Equal(product);
        var bad = () => test.Service.UpdateAsync(discount.Id, new("Percentage", 10, false, null, null, null, [Guid.NewGuid()], ""));
        await bad.Should().ThrowAsync<InvalidStateException>();
        var cleared = await test.Service.UpdateAsync(discount.Id, new("Percentage", 10, false, null, null, null, null, ""));
        cleared.EligibleProductIds.Should().BeNull();
    }

    [Fact]
    public async Task Detach_Should_DiscardOnlyFailedCartClaimAndItsDiscountMutation()
    {
        using var test = new Harness();
        var discount = await test.Service.CreateAsync(new("SAVE", DiscountKinds.Percentage, 10));
        var unrelated = await test.Service.CreateAsync(new("OTHER", DiscountKinds.Percentage, 20));
        var quote = await test.Service.ComputeAsync("SAVE", Lines(10), "GBP");
        var cart = Guid.NewGuid();
        await test.Service.ReserveTrackedAsync(cart, Guid.NewGuid(), "SAVE", Lines(10), "GBP", quote);
        var other = await test.Db.Discounts.SingleAsync(row => row.Id == unrelated.Id);
        other.IsActive = false;
        test.Service.Detach(cart, discount.Id);
        await test.Db.SaveChangesAsync();
        (await test.Db.DiscountReservations.CountAsync()).Should().Be(0);
        (await test.Db.Discounts.AsNoTracking().SingleAsync(row => row.Id == unrelated.Id)).IsActive.Should().BeFalse();
        (await test.Db.Discounts.AsNoTracking().SingleAsync(row => row.Id == discount.Id)).TimesRedeemed.Should().Be(0);
    }

    [Fact]
    public void Allocate_Should_KeepTinyManyLineDiscountNonnegativeAndExactlyReconciled()
    {
        var weights = Enumerable.Repeat(0.01m, 200).ToArray();
        var allocations = DiscountAllocationMath.Allocate(0.01m, weights);
        allocations.Sum().Should().Be(0.01m);
        allocations.Should().OnlyContain(value => value >= 0 && value <= 0.01m);
        DiscountAllocationMath.Allocate(100m, [1000m, 1000m, 1000m]).Should().Equal(33.3334m, 33.3333m, 33.3333m);
    }

    [Fact]
    public async Task ComputeAndReserve_Should_RejectAmountsThatCannotBeStoredWithoutRounding()
    {
        using var test = new Harness();
        var eligible = await test.ProductAsync("Eligible");
        var other = await test.ProductAsync("Other");
        var discount = await test.Service.CreateAsync(new("PRECISE", DiscountKinds.FixedAmount, 2m, "GBP",
            EligibleProductIds: [eligible]));
        var unrepresentable = 1.2345m * 0.81m;
        DiscountChargeLine[] lines = [new(0, eligible, unrepresentable), new(1, other, 10m)];
        var compute = () => test.Service.ComputeAsync("PRECISE", lines, "GBP");
        await compute.Should().ThrowAsync<InvalidStateException>().WithMessage("*four decimal places*");
        var reserve = () => test.Service.ReserveTrackedAsync(Guid.NewGuid(), Guid.NewGuid(), "PRECISE", lines, "GBP",
            new DiscountComputation(discount.Id, "PRECISE", unrepresentable, [new(0, unrepresentable)]));
        await reserve.Should().ThrowAsync<InvalidStateException>().WithMessage("*four decimal places*");
        await test.Db.SaveChangesAsync();
        (await test.Db.DiscountReservations.CountAsync()).Should().Be(0);
        (await test.Db.Discounts.AsNoTracking().SingleAsync()).TimesRedeemed.Should().Be(0);
    }

    [Theory]
    [InlineData(CartCheckoutStates.Preparing, false)]
    [InlineData(CartCheckoutStates.AwaitingPayment, false)]
    [InlineData(CartCheckoutStates.Preparing, true)]
    [InlineData(CartCheckoutStates.AwaitingPayment, true)]
    public async Task LegacyPreparation_Should_OccupyTheLastUseUntilConfirmedClosure(string state, bool paid)
    {
        using var test = new Harness();
        var discount = await test.Service.CreateAsync(new("SAVE", DiscountKinds.Percentage, 10, MaxRedemptions: 1));
        var quote = await test.Service.ComputeAsync("SAVE", Lines(20), "GBP");
        var cart = LegacyCart(test.TenantId, discount.Id, state);
        test.Db.Carts.Add(cart); await test.Db.SaveChangesAsync();
        var listed = (await test.Service.ListAsync(new())).Items.Single();
        listed.ReservedCount.Should().Be(1);
        var compute = () => test.Service.ComputeAsync("SAVE", Lines(20), "GBP");
        (await compute.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.AlreadyUsed);
        var reserve = () => test.Service.ReserveTrackedAsync(Guid.NewGuid(), Guid.NewGuid(), "SAVE", Lines(20), "GBP", quote);
        (await reserve.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.AlreadyUsed);
        var lower = () => test.Service.UpdateAsync(discount.Id, new("Percentage", 10, true, null, 0, null, null, ""));
        await lower.Should().ThrowAsync<InvalidStateException>();
        if (paid)
        {
            cart.Status = CartStatuses.CheckedOut;
            await test.Service.MarkRedeemedAsync(discount.Id);
        }
        else
        {
            cart.CheckoutState = CartCheckoutStates.Retryable;
            await test.Db.SaveChangesAsync();
        }
        var closed = (await test.Service.ListAsync(new())).Items.Single();
        closed.ReservedCount.Should().Be(0);
        closed.TimesRedeemed.Should().Be(paid ? 1 : 0);
        if (!paid) (await test.Service.ComputeAsync("SAVE", Lines(20), "GBP")).Amount.Should().Be(2m);
        (await test.Db.DiscountReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LegacyCount_Should_NotDuplicateCurrentReservationOrAlreadyCountedSummaryOnlyCheckout()
    {
        using var test = new Harness();
        var discount = await test.Service.CreateAsync(new("SAVE", DiscountKinds.Percentage, 10, MaxRedemptions: 3));
        var cart = LegacyCart(test.TenantId, discount.Id, CartCheckoutStates.Preparing);
        var preparation = CheckoutPreparation.Read(cart);
        test.Db.Carts.AddRange(cart, new Cart { TenantId = test.TenantId, Currency = "GBP", OrderId = Guid.NewGuid() });
        test.Db.DiscountReservations.Add(new DiscountReservation { TenantId = test.TenantId, CartId = cart.Id,
            DiscountId = discount.Id, AttemptId = preparation.AttemptId });
        await test.Db.SaveChangesAsync();
        await test.Service.MarkRedeemedAsync(discount.Id); // Pre-#344 submission already recorded this use.
        var listed = (await test.Service.ListAsync(new())).Items.Single();
        listed.ReservedCount.Should().Be(1);
        listed.TimesRedeemed.Should().Be(1);
        (await test.Service.ComputeAsync("SAVE", Lines(20), "GBP")).Amount.Should().Be(2m);
    }

    private static Cart LegacyCart(Guid tenantId, Guid discountId, string state) => new()
    {
        TenantId = tenantId, Currency = "GBP", CheckoutState = state,
        CheckoutPreparationJson = new CheckoutPreparation(Guid.NewGuid(), null, "GBP", "Stripe", "Card",
            null, null, null, 20m, 2m, discountId, "SAVE", 0m, 18m, [], [], [], [], null).Serialize()
    };

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; }
        public CommerceDbContext Db { get; }
        public CommerceTestHarness.TestClock Clock { get; } = new();
        public DiscountService Service { get; }
        public Harness()
        {
            var (options, tenantId) = CommerceTestHarness.NewDb();
            TenantId = tenantId;
            Db = CommerceTestHarness.CreateContext(options, tenantId, Clock);
            Service = new(Db, new TestTenantProvider(tenantId), Clock);
        }
        public async Task<Guid> ProductAsync(string name)
        {
            var product = new Product { TenantId = TenantId, Name = name, Slug = name.ToLowerInvariant() };
            Db.Products.Add(product); await Db.SaveChangesAsync(); return product.Id;
        }
        public void Dispose() => Db.Dispose();
    }
}
