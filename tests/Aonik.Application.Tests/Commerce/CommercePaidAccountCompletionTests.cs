using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Application.Tests.Commerce;

public class CommercePaidAccountCompletionTests
{
    [Fact]
    public async Task Completion_Should_RetryAccessIssuanceBeforeSendingReceipt()
    {
        var completed = new PaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 95, "GBP");
        var calls = new List<string>();
        var checkout = new Mock<ICheckoutService>();
        checkout.Setup(service => service.ConfirmPaymentAsync(completed.OrderId!.Value, completed.PaymentId,
                completed.Amount, completed.Currency, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("confirm")).ReturnsAsync(true);
        var access = new Mock<IPaidCheckoutAccountAccessService>();
        var fail = true;
        access.Setup(service => service.IssueAsync(completed.OrderId!.Value, completed.PaymentId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls.Add("access");
                return fail ? Task.FromException(new InvalidOperationException("Issuance unavailable")) : Task.CompletedTask;
            });
        var email = new Mock<IOrderConfirmationEmailService>();
        email.Setup(service => service.SendAsync(completed.OrderId!.Value, completed.PaymentId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("receipt")).Returns(Task.CompletedTask);
        var handler = new CommercePaymentCompletedHandler(checkout.Object, email.Object, access.Object,
            NullLogger<CommercePaymentCompletedHandler>.Instance);

        var first = () => handler.HandleAsync(completed);
        await first.Should().ThrowAsync<InvalidOperationException>().WithMessage("Issuance unavailable");
        calls.Should().Equal("confirm", "access");
        fail = false;
        await handler.HandleAsync(completed);

        calls.Should().Equal("confirm", "access", "confirm", "access", "receipt");
    }

    [Fact]
    public async Task Completion_Should_NotIssueAccessOrSendReceiptForRejectedPayment()
    {
        var checkout = new Mock<ICheckoutService>();
        var access = new Mock<IPaidCheckoutAccountAccessService>(MockBehavior.Strict);
        var email = new Mock<IOrderConfirmationEmailService>(MockBehavior.Strict);
        var handler = new CommercePaymentCompletedHandler(checkout.Object, email.Object, access.Object,
            NullLogger<CommercePaymentCompletedHandler>.Instance);

        await handler.HandleAsync(new PaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 95, "GBP"));
        await handler.HandleAsync(new PaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), null, 95, "GBP"));

        access.VerifyNoOtherCalls();
        email.VerifyNoOtherCalls();
    }
}
