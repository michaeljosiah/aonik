using System.Text.Json.Nodes;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Commerce;

public class CheckoutAccountConsentTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Checkout_Should_FreezeGuestConsent_AndIssueOnlyAfterExactPayment(bool optedIn, bool signedIn)
    {
        var (harness, box, result) = await PendingAsync(optedIn, signedIn ? Guid.NewGuid() : null);
        await using var context = harness.Commerce();
        var cart = await context.Carts.SingleAsync();
        var frozen = CheckoutPreparation.Read(cart);
        frozen.CreateAccount.Should().Be(optedIn && !signedIn);
        // An unrelated later form change cannot rewrite paid consent or its delivery recipient.
        cart.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(
            CreateAccount: !optedIn, Purchaser: new("later@example.test", "Later", "Buyer", "")));
        await context.SaveChangesAsync();
        var requests = new List<PaidAccountAccessRequest>();
        var issuer = new Mock<IPaidAccountAccessService>();
        issuer.Setup(service => service.IssueAsync(It.IsAny<PaidAccountAccessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaidAccountAccessRequest, CancellationToken>((request, _) => requests.Add(request))
            .Returns(Task.CompletedTask);
        var access = new PaidCheckoutAccountAccessService(context, new TestTenantProvider(harness.TenantId),
            new CoreOrderService(harness.Ordering(), new TestTenantProvider(harness.TenantId), harness.Clock,
                new TestCurrentUserProvider()), issuer.Object);
        var email = new Mock<IOrderConfirmationEmailService>();
        var handler = new CommercePaymentCompletedHandler(harness.Checkout(), email.Object, access,
            NullLogger<CommercePaymentCompletedHandler>.Instance);

        await access.IssueAsync(result.OrderId, result.PaymentIntentId);
        requests.Should().BeEmpty("an uncompleted payment cannot issue account access");
        await handler.HandleAsync(new PaymentCompletedEvent(harness.TenantId, Guid.NewGuid(), result.OrderId,
            result.Total, result.Currency));
        requests.Should().BeEmpty("another payment attempt cannot authorize account access");
        await handler.HandleAsync(new PaymentCompletedEvent(harness.TenantId, result.PaymentIntentId, result.OrderId,
            result.Total, result.Currency));

        if (optedIn && !signedIn)
        {
            var request = requests.Should().ContainSingle().Subject;
            request.Should().Be(new PaidAccountAccessRequest(harness.TenantId, box.Box.CartId, result.OrderId,
                result.PaymentIntentId, frozen.GuestPartyId!.Value, BoxTestHarness.ValidDelivery.Purchaser.Email));
        }
        else requests.Should().BeEmpty();
        email.Verify(service => service.SendAsync(result.OrderId, result.PaymentIntentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Recovery_Should_FreezeTheNewConsentOnlyForTheReplacementAttempt(bool firstConsent, bool secondConsent)
    {
        var (harness, box, first) = await PendingAsync(firstConsent);
        var state = await harness.Checkout().GetPaymentStateAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));
        var recovered = await harness.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId,
            CartAccessContext.ForGuest(box.CartToken, state.CartVersion));
        var saved = await harness.Carts().SaveCheckoutDraftAsync(box.Box.CartId,
            new CartCheckoutDraftDto(CreateAccount: secondConsent), CartAccessContext.ForGuest(box.CartToken, recovered.CartVersion));
        var access = await harness.HoldDeliveryAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken, saved.CartVersion));

        var second = await harness.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card",
            Delivery: BoxTestHarness.ValidDelivery), access);

        second.OrderId.Should().Be(first.OrderId);
        second.PaymentIntentId.Should().NotBe(first.PaymentIntentId);
        await using var context = harness.Commerce();
        var preparation = CheckoutPreparation.Read(await context.Carts.SingleAsync());
        preparation.AttemptId.Should().Be(second.PaymentIntentId);
        preparation.CreateAccount.Should().Be(secondConsent);
        (await harness.Checkout().ConfirmPaymentAsync(first.OrderId, first.PaymentIntentId, first.Total, first.Currency))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Preparation_Should_TreatOlderSnapshotsWithoutConsentAsOptedOut()
    {
        var (harness, _, _) = await PendingAsync(true);
        await using var context = harness.Commerce();
        var cart = await context.Carts.SingleAsync();
        var json = JsonNode.Parse(cart.CheckoutPreparationJson!)!.AsObject();
        json.Remove("createAccount");
        cart.CheckoutPreparationJson = json.ToJsonString();

        CheckoutPreparation.Read(cart).CreateAccount.Should().BeFalse();
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, CheckoutResult Result)> PendingAsync(
        bool createAccount, Guid? partyId = null)
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("jollof");
        var created = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6, BuyerPartyId: partyId));
        var access = partyId is { } party ? CartAccessContext.ForParty(party, created.CartVersion)
            : CartAccessContext.ForGuest(created.CartToken, created.CartVersion);
        var full = await harness.BoxCarts().AddLineAsync(created.Box.CartId,
            new AddBoxLineCommand(fixture.DishVariants["jollof"], 6, null), access);
        var saved = await harness.Carts().SaveCheckoutDraftAsync(created.Box.CartId,
            new CartCheckoutDraftDto(CreateAccount: createAccount), access with { ExpectedCartVersion = full.CartVersion });
        access = await harness.HoldDeliveryAsync(created.Box.CartId, access with { ExpectedCartVersion = saved.CartVersion });
        var checkout = await harness.Checkout().CheckoutAsync(new CheckoutCommand(created.Box.CartId, "Stripe", "Card",
            Delivery: BoxTestHarness.ValidDelivery), access);
        return (harness, full with { CartToken = created.CartToken, CartVersion = access.ExpectedCartVersion! }, checkout);
    }
}
