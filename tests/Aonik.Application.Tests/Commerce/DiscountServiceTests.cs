using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Promotions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Discount/coupon validation + computation (Spec 042 §5 follow-up).</summary>
public class DiscountServiceTests
{
    private static DiscountService NewService(out Guid tenantId)
    {
        var (options, t) = CommerceTestHarness.NewDb();
        tenantId = t;
        return new DiscountService(CommerceTestHarness.CreateContext(options, t), new TestTenantProvider(t), new CommerceTestHarness.TestClock());
    }

    [Fact]
    public async Task Percentage_Should_ComputeProportionOfSubtotal()
    {
        var svc = NewService(out _);
        await svc.CreateAsync(new CreateDiscountCommand("SAVE10", DiscountKinds.Percentage, 10m));

        var result = await svc.ComputeAsync("SAVE10", 5_000m, "NGN");

        result.Amount.Should().Be(500m);
        result.Code.Should().Be("SAVE10");
    }

    [Fact]
    public async Task FixedAmount_Should_ComputeFlatAmount_ForMatchingCurrency()
    {
        var svc = NewService(out _);
        await svc.CreateAsync(new CreateDiscountCommand("FLAT1000", DiscountKinds.FixedAmount, 1_000m, Currency: "NGN"));

        (await svc.ComputeAsync("FLAT1000", 5_000m, "NGN")).Amount.Should().Be(1_000m);

        var wrongCurrency = async () => await svc.ComputeAsync("FLAT1000", 5_000m, "USD");
        await wrongCurrency.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Compute_Should_ReturnZero_ForNullCode()
        => (await NewService(out _).ComputeAsync(null, 5_000m, "NGN")).Amount.Should().Be(0m);

    [Fact]
    public async Task Compute_Should_NotExceedSubtotal()
    {
        var svc = NewService(out _);
        await svc.CreateAsync(new CreateDiscountCommand("BIG", DiscountKinds.FixedAmount, 9_999m, Currency: "NGN"));
        (await svc.ComputeAsync("BIG", 5_000m, "NGN")).Amount.Should().Be(5_000m);
    }

    [Fact]
    public async Task Compute_Should_Throw_WhenExpired()
    {
        var svc = NewService(out _);
        await svc.CreateAsync(new CreateDiscountCommand("OLD", DiscountKinds.Percentage, 10m,
            ExpiresAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var act = async () => await svc.ComputeAsync("OLD", 5_000m, "NGN");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Compute_Should_Throw_WhenRedemptionLimitReached()
    {
        var svc = NewService(out _);
        var created = await svc.CreateAsync(new CreateDiscountCommand("ONCE", DiscountKinds.Percentage, 10m, MaxRedemptions: 1));
        await svc.MarkRedeemedAsync(created.Id);

        var act = async () => await svc.ComputeAsync("ONCE", 5_000m, "NGN");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreatePercentage_Should_Reject_OutOfRangeValue()
    {
        var svc = NewService(out _);
        var act = async () => await svc.CreateAsync(new CreateDiscountCommand("BAD", DiscountKinds.Percentage, 150m));
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
