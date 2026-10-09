using System.Text.Json;

using FluentAssertions;
using Moq;

using Aonik.Commerce.Agents;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Application.Tests.Commerce;

public class CommerceCheckoutProposalHandlerTests
{
    [Fact]
    public async Task ApprovedCheckout_Should_NotResumeAnAttemptThatAnotherRequestAlreadyClaimed()
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("jollof");
        var box = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6));
        var filled = await harness.BoxCarts().AddLineAsync(box.Box.CartId,
            new AddBoxLineCommand(fixture.DishVariants["jollof"], 6, null),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));
        var approvedAccess = CartAccessContext.ForGuest(box.CartToken, filled.CartVersion);
        await harness.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card",
            Delivery: BoxTestHarness.ValidDelivery), approvedAccess);

        await harness.Checkout().Invoking(c => c.CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card",
                Delivery: BoxTestHarness.ValidDelivery, RequireFreshCart: true), approvedAccess))
            .Should().ThrowAsync<CartWriteConflictException>();

        harness.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_ExecutePreciselyApprovedCartVersion()
    {
        var cartId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var checkout = new Mock<ICheckoutService>(MockBehavior.Strict);
        var approvedCommand = new CheckoutCommand(cartId, "Stripe", "Card", RequireFreshCart: true);
        checkout.Setup(c => c.CheckoutAsync(approvedCommand,
                CartAccessContext.ForGuest("guest-token", "approved-version"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutResult(orderId, null, Guid.NewGuid(), "Pending", 20, 0, 0, 20, "GBP"));
        var payload = JsonSerializer.Serialize(new { cartId, provider = "Stripe", paymentMethodType = "Card",
            cartToken = "guest-token", expectedCartVersion = "approved-version" });

        var result = await new CommerceCheckoutProposalHandler(checkout.Object).HandleAsync(Proposal(payload), default);

        result.Applied.Should().BeTrue();
        result.AppliedResourceId.Should().Be(orderId);
        checkout.VerifyAll();
        checkout.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{}")] [InlineData("invalid json")] [InlineData("null")]
    public async Task HandleAsync_Should_RejectMissingApprovalInputs_WithoutCheckout(string payload)
    {
        var checkout = new Mock<ICheckoutService>(MockBehavior.Strict);
        var result = await new CommerceCheckoutProposalHandler(checkout.Object).HandleAsync(Proposal(payload), default);
        result.Applied.Should().BeFalse();
        checkout.VerifyNoOtherCalls();
    }

    private static AgentProposalDetail Proposal(string payload) => new(Guid.NewGuid(), Guid.NewGuid(),
        CommerceCheckoutProposalHandler.ProposalTypeKey, "Approved", payload, null);
}
