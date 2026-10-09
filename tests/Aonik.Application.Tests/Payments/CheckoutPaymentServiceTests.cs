using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Payments;

public class CheckoutPaymentServiceTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Create_Should_RejectNonUtcDeadlineBeforeCreatingAttempt(DateTimeKind kind)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var request = test.Request with { ProviderStartDeadlineUtc = DateTime.SpecifyKind(test.Now.AddMinutes(1), kind) };

        await test.Service.Invoking(service => service.CreateAsync(request)).Should()
            .ThrowAsync<InvalidStateException>().WithMessage("*deadline must be UTC*");

        (await test.Db.PaymentIntents.CountAsync()).Should().Be(0);
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_Should_DurablyCloseMissingAttemptAtOrAfterDeadline_WithoutConfigurationOrProvider(int secondsUntilDeadline)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var request = test.Request with { ProviderStartDeadlineUtc = test.Now.AddSeconds(secondsUntilDeadline) };
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No connector is needed for proven no-start closure."));

        (await test.Service.GetStateAsync(test.AttemptId)).Should().BeNull();
        var result = await test.Service.CreateAsync(request);
        var replay = await test.Service.CreateAsync(request);
        var state = await test.Service.GetStateAsync(test.AttemptId);

        result.Status.Should().Be("Cancelled");
        replay.Should().Be(result);
        state!.CanNoLongerPay.Should().BeTrue();
        var intent = (await test.Db.PaymentIntents.AsNoTracking().ToListAsync()).Should().ContainSingle().Subject;
        intent.ProviderStartDeadlineUtc.Should().Be(request.ProviderStartDeadlineUtc);
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        intent.ProviderCreateRequestJson.Should().BeNull();
        intent.ConnectorId.Should().BeNull();
        test.Connectors.Verify(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>()), Times.Never);
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_PersistDeadlineBeforeStartingWithinItsWindow()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var request = test.Request with { ProviderStartDeadlineUtc = test.Now.AddTicks(1) };
        test.Gateway.OnCreate = async (providerRequest, ct) =>
        {
            var intent = await test.Db.PaymentIntents.AsNoTracking().SingleAsync(ct);
            intent.ProviderStartDeadlineUtc.Should().Be(request.ProviderStartDeadlineUtc);
            intent.ProviderRequestStartedAtUtc.Should().Be(test.Now);
            return test.Gateway.Result(providerRequest);
        };

        var result = await test.Service.CreateAsync(request);

        result.Status.Should().Be("Pending");
        test.Gateway.Requests.Should().ContainSingle();
        (await test.Service.GetStateAsync(test.AttemptId))!.CanNoLongerPay.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Should_RecheckDeadlineAfterConnectorResolutionBeforeClaimingProviderStart()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var deadline = test.Now.AddMinutes(1);
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            test.Clock.SetupGet(c => c.UtcNow).Returns(deadline);
            return Task.FromResult(test.Binding);
        });

        var result = await test.Service.CreateAsync(test.Request with { ProviderStartDeadlineUtc = deadline });

        result.Status.Should().Be("Cancelled");
        var intent = await test.Db.PaymentIntents.AsNoTracking().SingleAsync();
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        intent.ProviderCreateRequestJson.Should().BeNull();
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_CancelUnstartedConfigurationFailureAfterDeadline_InSameScope()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var deadline = test.Now.AddMinutes(1);
        var request = test.Request with { ProviderStartDeadlineUtc = deadline };
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        await test.Service.Invoking(s => s.CreateAsync(request)).Should().ThrowAsync<InvalidOperationException>();
        test.Clock.SetupGet(c => c.UtcNow).Returns(deadline);

        var result = await test.Service.CreateAsync(request);

        result.Status.Should().Be("Cancelled");
        (await test.Service.GetStateAsync(test.AttemptId))!.CanNoLongerPay.Should().BeTrue();
        test.Connectors.Verify(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>()), Times.Once);
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("extend")]
    [InlineData("shorten")]
    [InlineData("add")]
    public async Task Create_Should_RejectChangingImmutableDeadlineOnReplay(string change)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var request = test.Request with { ProviderStartDeadlineUtc = change == "add" ? null : test.Now.AddMinutes(1) };
        await test.Service.CreateAsync(request);
        var altered = request with
        {
            ProviderStartDeadlineUtc = change switch
            {
                "remove" => null,
                "shorten" => test.Now,
                _ => test.Now.AddMinutes(2)
            }
        };

        await test.Service.Invoking(s => s.CreateAsync(altered)).Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*different checkout details*");

        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).ProviderStartDeadlineUtc.Should().Be(request.ProviderStartDeadlineUtc);
    }

    [Fact]
    public async Task Create_Should_ResumeStartedTimeoutWithSameRequestAfterDeadline_WithoutClaimingClosure()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var request = test.Request with { ProviderStartDeadlineUtc = test.Now.AddMinutes(1) };
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();
        await test.Service.Invoking(s => s.CreateAsync(request)).Should().ThrowAsync<TimeoutException>();
        var original = test.Gateway.Requests.Single();
        test.Clock.SetupGet(c => c.UtcNow).Returns(test.Now.AddMinutes(2));
        test.Gateway.OnCreate = null;

        var result = await test.Service.CreateAsync(request);

        result.Status.Should().Be("Pending");
        test.Gateway.Requests.Should().Equal(original, original);
        (await test.Service.GetStateAsync(test.AttemptId))!.CanNoLongerPay.Should().BeFalse();
        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).ProviderRequestStartedAtUtc.Should().Be(test.Now);
    }

    [Fact]
    public async Task Create_Should_RejectSubminimumChargeBeforePersistingAttempt()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();

        await test.Service.Invoking(s => s.CreateAsync(test.Request with { Amount = 0.29m }))
            .Should().ThrowAsync<InvalidStateException>();

        test.Gateway.Requests.Should().BeEmpty();
        (await test.Db.PaymentIntents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_Should_DurablyBindActualPayerAndRequestBeforeProviderCall_ThenReplayWithoutAnotherCall()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = async (request, ct) =>
        {
            var durable = await test.Db.PaymentIntents.AsNoTracking().SingleAsync(ct);
            durable.Id.Should().Be(test.AttemptId);
            durable.PayerPartyId.Should().Be(test.PayerId);
            durable.Status.Should().Be("Processing");
            durable.ProviderAccountId.Should().Be("acct_merchant");
            durable.ProviderRequestStartedAtUtc.Should().Be(test.Now);
            JsonSerializer.Deserialize<PaymentProviderIntentRequest>(durable.ProviderCreateRequestJson!).Should().Be(request);
            return test.Gateway.Result(request);
        };

        var first = await test.Service.CreateAsync(test.Request);
        var replay = await test.Service.CreateAsync(test.Request);

        replay.Should().Be(first);
        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.PaymentIntents.CountAsync()).Should().Be(1);
        (await test.Db.Orders.SingleAsync()).Status.Should().Be("PendingFunding");
        first.ClientSecret.Should().BeNull();
    }

    [Fact]
    public async Task Create_Should_ResumeTimeoutWithSameKeyAndFrozenUrlsWithoutReselectingConnector()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();
        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<TimeoutException>();
        var original = test.Gateway.Requests.Single();
        test.Gateway.OnCreate = null;
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Must not reselect"));

        var result = await test.Service.CreateAsync(test.Request with { ReturnUrl = "https://other.example/changed" });

        test.Gateway.Requests.Should().Equal(original, original);
        result.PaymentIntentId.Should().Be(test.AttemptId);
        (await test.Db.PaymentIntents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_Should_LeaveSafeLocalPendingClaimWhenConfigurationIsUnavailable()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Not configured"));

        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidOperationException>();

        var intent = await test.Db.PaymentIntents.AsNoTracking().SingleAsync();
        intent.Status.Should().Be("Pending");
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        intent.ProviderReference.Should().BeNull();
        test.Gateway.Requests.Should().BeEmpty();

        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(test.Binding);
        var retry = await test.Service.CreateAsync(test.Request);
        retry.PaymentIntentId.Should().Be(test.AttemptId);
        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.PaymentIntents.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("https://shop.example/success#fragment")]
    [InlineData("https://shop.example/suc\ncess")]
    [InlineData("https://evil.example/success")]
    public async Task Create_Should_RejectBadReturnUrlBeforeProviderClaim(string url)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();

        await test.Service.Invoking(s => s.CreateAsync(test.Request with { ReturnUrl = url })).Should().ThrowAsync<InvalidStateException>();

        var intent = await test.Db.PaymentIntents.AsNoTracking().SingleAsync();
        intent.Status.Should().Be("Pending");
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_RejectUnknownAttemptsBeyondSafeIdempotencyWindow()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();
        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<TimeoutException>();
        test.Clock.SetupGet(c => c.UtcNow).Returns(test.Now.AddHours(23));

        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidStateException>().WithMessage("*reconciliation*");
        await test.Service.Invoking(s => s.ExpireAsync(test.AttemptId)).Should().ThrowAsync<InvalidStateException>().WithMessage("*reconciliation*");

        test.Gateway.Requests.Should().ContainSingle();
        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).Status.Should().Be("Processing");
        test.Reconciler.Verify(r => r.ReconcileAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("key")]
    [InlineData("currency")]
    public async Task Create_Should_RejectChangesToAttemptMoneyOrKey(string change)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        await test.Service.CreateAsync(test.Request);
        var altered = change switch
        {
            "amount" => test.Request with { Amount = 42.51m },
            "currency" => test.Request with { Currency = "USD" },
            _ => test.Request with { IdempotencyKey = "replacement-key" },
        };

        await test.Service.Invoking(s => s.CreateAsync(altered)).Should().ThrowAsync<InvalidStateException>();

        test.Gateway.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Expire_Should_RecoverTimedOutCreateWithSameFrozenRequestBeforeReconciling()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();
        var request = test.Request with { ProviderStartDeadlineUtc = test.Now.AddMinutes(1) };
        await test.Service.Invoking(s => s.CreateAsync(request)).Should().ThrowAsync<TimeoutException>();
        var original = test.Gateway.Requests.Single();
        test.Clock.SetupGet(c => c.UtcNow).Returns(test.Now.AddMinutes(2));
        test.Gateway.OnCreate = null;
        test.Reconciler.Setup(r => r.ReconcileAsync(test.AttemptId, true, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Aonik.SharedKernel.Abstractions.Payments.PaymentIntentStateRef(
                test.AttemptId, test.OrderId, 42.50m, "GBP", "Cancelled", true));

        var result = await test.Service.ExpireAsync(test.AttemptId);

        result.CanNoLongerPay.Should().BeTrue();
        test.Gateway.Requests.Should().Equal(original, original);
        test.Reconciler.Verify(r => r.ReconcileAsync(test.AttemptId, true, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Should_RejectCorruptFrozenRequestBeforeRetryingProvider()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = (_, _) => throw new TimeoutException();
        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<TimeoutException>();
        var intent = await test.Db.PaymentIntents.SingleAsync();
        var frozen = JsonSerializer.Deserialize<PaymentProviderIntentRequest>(intent.ProviderCreateRequestJson!)!;
        intent.ProviderCreateRequestJson = JsonSerializer.Serialize(frozen with { Amount = 900m });
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();

        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidStateException>().WithMessage("*does not match*");

        test.Gateway.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_Should_RequireActualPurchaserBeforeAnyAttemptOrProviderCall()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync(includePayer: false);

        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidStateException>().WithMessage("*actual purchaser*");

        test.Db.PaymentIntents.Should().BeEmpty();
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_EnforceTenantEvenWhenContextFilterWouldAllowOrder()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var other = new CheckoutPaymentService(test.Db, new TestTenantProvider(Guid.NewGuid()), test.Connectors.Object,
            [test.Gateway], test.Reconciler.Object, test.Clock.Object, NullLogger<CheckoutPaymentService>.Instance);

        await other.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<NotFoundException>();
        (await other.GetStateAsync(test.AttemptId)).Should().BeNull();
        test.Gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_NotTrustProviderResultStatusToBypassVerifiedReconciliation()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Gateway.OnCreate = (request, _) => Task.FromResult(test.Gateway.Result(request) with { Status = "Captured", Checkout = null });

        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidStateException>();

        (await test.Db.PaymentIntents.AsNoTracking().SingleAsync()).Status.Should().Be("Processing");
        test.Db.Payments.Should().BeEmpty();
        test.Reconciler.Verify(r => r.ApplyAsync(It.IsAny<Guid>(), It.IsAny<PaymentProviderCheckoutSnapshot>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelledAttempt_Should_NeverStartAnotherProviderRequestOnCreateOrExpireReplay()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        test.Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        await test.Service.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<InvalidOperationException>();
        var intent = await test.Db.PaymentIntents.SingleAsync();
        intent.Status = "Cancelled";
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();

        var replay = await test.Service.CreateAsync(test.Request);
        var state = await test.Service.ExpireAsync(test.AttemptId);

        replay.Status.Should().Be("Cancelled");
        state.CanNoLongerPay.Should().BeTrue();
        test.Gateway.Requests.Should().BeEmpty();
        test.Reconciler.Verify(r => r.ReconcileAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
