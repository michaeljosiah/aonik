using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Events.Integration;

using FluentAssertions;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class LoyaltyOrderProjectionTests
{
    [Theory]
    [InlineData(false, false, "Captured", null, "AccountNotLinked")]
    [InlineData(false, true, "Captured", 180L, "AccountSetupRequired")]
    [InlineData(true, false, "Captured", 180L, "Earned")]
    [InlineData(true, false, "Pending", null, "NotPaid")]
    public void Order_Should_ShowPostedEarningOnlyForAnAccountOrConsentedSetup(
        bool linked, bool optedIn, string paymentStatus, long? points, string status)
    {
        var cart = new Cart { BuyerPartyId = linked ? Guid.NewGuid() : null };
        if (optedIn)
            cart.CheckoutPreparationJson = new CheckoutPreparation(Guid.NewGuid(), Guid.NewGuid(), "GBP", "Stripe", "Card",
                null, null, null, 100, 0, null, null, 0, 90, [], [], [], [], null, CreateAccount: true).Serialize();
        var snapshot = new LoyaltyCheckout(cart.Id, cart.BuyerPartyId ?? Guid.NewGuid(), !linked, "policy",
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), 1000, 180, 10, 100, [], 90);
        var summary = new OrderChargeSummary { PaymentStatus = paymentStatus, LoyaltyJson = CheckoutLoyaltyData.Serialize(snapshot) };

        var result = CheckoutLoyaltyData.ForOrder(summary, cart)!;

        result.EarnedPoints.Should().Be(points);
        result.EarningStatus.Should().Be(status);
        result.AppliedValue.Should().Be(10);
    }

    [Fact]
    public async Task VerifiedHandler_Should_RetryTheLedgerTransfer_AfterAnAlreadyLinkedCart()
    {
        var access = new Mock<IPaidCheckoutAccountAccessService>();
        var loyalty = new Mock<ILoyaltyService>();
        var verified = new AccountAccessVerifiedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var order = new List<string>();
        access.Setup(service => service.LinkAsync(verified, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("verified")).Returns(Task.CompletedTask);
        loyalty.SetupSequence(service => service.AttachVerifiedGuestAsync(verified, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Lost ledger response"))
            .Returns(Task.CompletedTask);
        var handler = new CommerceAccountAccessVerifiedHandler(access.Object, loyalty.Object);
        Func<Task> first = () => handler.HandleAsync(verified);
        await first.Should().ThrowAsync<InvalidOperationException>();
        await handler.HandleAsync(verified);

        order.Should().Equal("verified", "verified");
        loyalty.Verify(service => service.AttachVerifiedGuestAsync(verified, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task VerifiedHandler_Should_NotTransfer_WhenPaidSourceVerificationFails()
    {
        var access = new Mock<IPaidCheckoutAccountAccessService>();
        var loyalty = new Mock<ILoyaltyService>(MockBehavior.Strict);
        access.Setup(service => service.LinkAsync(It.IsAny<AccountAccessVerifiedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Another owner"));
        var handler = new CommerceAccountAccessVerifiedHandler(access.Object, loyalty.Object);
        var verified = new AccountAccessVerifiedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Func<Task> action = () => handler.HandleAsync(verified);
        await action.Should().ThrowAsync<InvalidOperationException>();

        loyalty.VerifyNoOtherCalls();
    }
}
