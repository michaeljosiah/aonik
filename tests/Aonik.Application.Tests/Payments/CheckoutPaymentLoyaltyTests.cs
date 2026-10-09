using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;

namespace Aonik.Application.Tests.Payments;

public sealed class CheckoutPaymentLoyaltyTests
{
    [Fact]
    public async Task Create_Should_DurablyReserveBeforeProvider_AndNotReserveAgainOnReplay()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync();
        test.Gateway.OnCreate = async (request, ct) =>
        {
            await using var scope = test.NewScope();
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var hold = await db.LoyaltyCheckoutAttempts.SingleAsync(ct);
            hold.PaymentIntentId.Should().Be(test.AttemptId);
            hold.ReservedPoints.Should().Be(100);
            hold.Status.Should().Be("Reserved");
            (await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(test.PayerId, ct))
                .AvailablePoints.Should().Be(0);
            return test.Gateway.Result(request);
        };

        var result = await test.Service.CreateAsync(test.Request with { Loyalty = loyalty });
        var replay = await test.Service.CreateAsync(test.Request with { Loyalty = loyalty });

        replay.Should().Be(result);
        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.LoyaltyCheckoutAttempts.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_Should_PersistCancelledOnKnownRejection_AndNeverReviveSameAttempt(bool changedPolicy)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync(balance: changedPolicy ? 100 : 0);
        if (changedPolicy)
            test.Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

        var result = await test.Service.CreateAsync(test.Request with { Loyalty = loyalty });
        test.Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new LoyaltyPolicy(true, "test-policy", loyalty.Ledger),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        if (!changedPolicy)
        {
            await using var funding = test.NewScope();
            await funding.ServiceProvider.GetRequiredService<LoyaltyService>().AdjustAsync(new(test.PayerId, Guid.NewGuid(), 100, "Balance recovered"));
        }
        var replay = await test.Service.CreateAsync(test.Request with { Loyalty = loyalty });

        result.Status.Should().Be("Cancelled");
        replay.Should().Be(result);
        (await test.Service.GetStateAsync(test.AttemptId))!.CanNoLongerPay.Should().BeTrue();
        var intent = await test.Db.PaymentIntents.AsNoTracking().SingleAsync();
        intent.FailureReason.Should().NotBeNullOrWhiteSpace();
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        (await test.Db.LoyaltyCheckoutAttempts.AsNoTracking().SingleAsync()).Status.Should().Be("Released");
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_RejectRemovingOrChangingFrozenLoyalty()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync();
        var request = test.Request with { Loyalty = loyalty };
        await test.Service.CreateAsync(request);

        await test.Service.Invoking(s => s.CreateAsync(request with { Loyalty = null })).Should().ThrowAsync<InvalidStateException>();
        await test.Service.Invoking(s => s.CreateAsync(request with { Loyalty = loyalty with { CartId = Guid.NewGuid() } }))
            .Should().ThrowAsync<InvalidStateException>();
        test.Gateway.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_Should_RejectAddingLoyaltyToAnExistingPlainAttempt()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync();
        await test.Service.CreateAsync(test.Request);

        await test.Service.Invoking(s => s.CreateAsync(test.Request with { Loyalty = loyalty })).Should().ThrowAsync<InvalidStateException>();

        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.LoyaltyCheckoutAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpiredCreate_Should_PersistFrozenReleasedInstruction_WithoutCurrentPolicy()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync(balance: 0);
        test.Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Policy store unavailable"));
        var request = test.Request with { Loyalty = loyalty, ProviderStartDeadlineUtc = test.Now };

        (await test.Service.CreateAsync(request)).Status.Should().Be("Cancelled");
        (await test.Service.CreateAsync(request)).Status.Should().Be("Cancelled");

        (await test.Db.LoyaltyCheckoutAttempts.AsNoTracking().SingleAsync()).Status.Should().Be("Released");
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderClaim_Should_ReleaseReservation_WhenDeadlinePassesDuringConnectorResolution()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync();
        var deadline = test.Now.AddMinutes(1);
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            test.Clock.SetupGet(c => c.UtcNow).Returns(deadline);
            return Task.FromResult(test.Binding);
        });

        (await test.Service.CreateAsync(test.Request with { Loyalty = loyalty, ProviderStartDeadlineUtc = deadline }))
            .Status.Should().Be("Cancelled");

        await using var scope = test.NewScope();
        var balance = await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(test.PayerId);
        balance.BalancePoints.Should().Be(100);
        balance.ReservedPoints.Should().Be(0);
        (await test.Db.LoyaltyCheckoutAttempts.AsNoTracking().SingleAsync()).Status.Should().Be("Released");
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_RetainReservation_WhenProviderOutcomeIsUnknown()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var loyalty = await test.SeedLoyaltyAsync();
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();

        await test.Service.Invoking(s => s.CreateAsync(test.Request with { Loyalty = loyalty })).Should().ThrowAsync<TimeoutException>();

        await using var scope = test.NewScope();
        (await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(test.PayerId)).ReservedPoints.Should().Be(100);
        (await test.Service.GetStateAsync(test.AttemptId))!.CanNoLongerPay.Should().BeFalse();
    }
}
