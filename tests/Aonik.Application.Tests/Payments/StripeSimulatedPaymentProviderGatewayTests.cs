using FluentAssertions;

using Aonik.Application.Services.Payments;

namespace Aonik.Application.Tests.Payments;

public class StripeSimulatedPaymentProviderGatewayTests
{
    [Fact]
    public async Task CreateIntentAsync_ShouldAppendProviderParameters_WithQuestionMark_WhenReturnUrlHasNoQuery()
    {
        // Arrange
        var gateway = new StripeSimulatedPaymentProviderGateway();
        var request = new PaymentProviderIntentRequest(
            Guid.NewGuid(),
            100m,
            "USD",
            "Card",
            "https://localhost:5174/payments/return",
            null,
            "ORD-1");

        // Act
        var result = await gateway.CreateIntentAsync(request);

        // Assert
        result.CheckoutUrl.Should().StartWith("https://localhost:5174/payments/return?provider=stripe&payment_intent=pi_");
    }

    [Fact]
    public async Task CreateIntentAsync_ShouldAppendProviderParameters_WithAmpersand_WhenReturnUrlHasQuery()
    {
        // Arrange
        var gateway = new StripeSimulatedPaymentProviderGateway();
        var request = new PaymentProviderIntentRequest(
            Guid.NewGuid(),
            100m,
            "USD",
            "Card",
            "https://localhost:5174/payments/return?orderId=abc&result=cancelled",
            null,
            "ORD-1");

        // Act
        var result = await gateway.CreateIntentAsync(request);

        // Assert
        result.CheckoutUrl.Should().StartWith(
            "https://localhost:5174/payments/return?orderId=abc&result=cancelled&provider=stripe&payment_intent=pi_");
    }
}
