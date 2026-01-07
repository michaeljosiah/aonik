using Aonik.Domain.Payments.Entities;
using Aonik.Domain.Payments;
using FluentAssertions;

namespace Aonik.Domain.Tests.Payments;

public class PaymentIntentTests
{
    [Fact]
    public void Constructor_ShouldCreatePaymentIntentWithPendingStatus()
    {
        // Arrange & Act
        var paymentIntent = new PaymentIntent(
            amount: 100.00m,
            currency: "USD",
            reference: "PAY-001");

        // Assert
        paymentIntent.Should().NotBeNull();
        paymentIntent.Id.Should().NotBeEmpty();
        paymentIntent.Status.Should().Be(PaymentStatus.Pending);
        paymentIntent.Amount.Should().Be(100.00m);
        paymentIntent.Currency.Should().Be("USD");
        paymentIntent.Reference.Should().Be("PAY-001");
        paymentIntent.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Authorize_ShouldChangeStatusToAuthorized_WhenPending()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");

        // Act
        paymentIntent.Authorize();

        // Assert
        paymentIntent.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public void Authorize_ShouldThrow_WhenNotPending()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");
        paymentIntent.Authorize();

        // Act
        var act = () => paymentIntent.Authorize();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only pending payments can be authorized");
    }

    [Fact]
    public void Capture_ShouldChangeStatusToCaptured_WhenAuthorized()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");
        paymentIntent.Authorize();

        // Act
        paymentIntent.Capture();

        // Assert
        paymentIntent.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public void Capture_ShouldThrow_WhenNotAuthorized()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");

        // Act
        var act = () => paymentIntent.Capture();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only authorized payments can be captured");
    }

    [Fact]
    public void Fail_ShouldChangeStatusToFailed_WhenPendingOrAuthorized()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");

        // Act
        paymentIntent.Fail();

        // Assert
        paymentIntent.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Fail_ShouldThrow_WhenCaptured()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");
        paymentIntent.Authorize();
        paymentIntent.Capture();

        // Act
        var act = () => paymentIntent.Fail();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Captured payments cannot be marked as failed");
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled_WhenPendingOrAuthorized()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");
        paymentIntent.Authorize();

        // Act
        paymentIntent.Cancel();

        // Assert
        paymentIntent.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenCaptured()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");
        paymentIntent.Authorize();
        paymentIntent.Capture();

        // Act
        var act = () => paymentIntent.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Captured payments cannot be cancelled");
    }

    [Fact]
    public void PaymentWorkflow_ShouldFollowHappyPath()
    {
        // Arrange
        var paymentIntent = new PaymentIntent(100.00m, "USD", "PAY-001");

        // Act & Assert - Pending -> Authorized -> Captured
        paymentIntent.Status.Should().Be(PaymentStatus.Pending);
        
        paymentIntent.Authorize();
        paymentIntent.Status.Should().Be(PaymentStatus.Authorized);
        
        paymentIntent.Capture();
        paymentIntent.Status.Should().Be(PaymentStatus.Captured);
    }
}
