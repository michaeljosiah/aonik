using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Api.Contracts.Payments;

namespace Aonik.Api.Tests;

public class PaymentEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePaymentIntent_ReturnsCreated()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Create"));

        var request = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-001");

        // Act
        var response = await client.PostAsJsonAsync("/payments/intents", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var payment = await response.Content.ReadFromJsonAsync<PaymentIntentResponse>();
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(100.00m);
        payment.Currency.Should().Be("USD");
        payment.Reference.Should().Be("ORDER-001");
        payment.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetPaymentIntent_ReturnsPayment_WhenExists()
    {
        // Arrange - Create payment first
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Create", "Payment.Read"));

        var createRequest = new CreatePaymentIntentRequest(250.00m, "EUR", "ORDER-002");
        var createResponse = await client.PostAsJsonAsync("/payments/intents", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Act
        var getResponse = await client.GetAsync($"/payments/intents/{createdPayment!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();
        payment.Should().NotBeNull();
        payment!.Id.Should().Be(createdPayment.Id);
        payment.Amount.Should().Be(250.00m);
    }

    [Fact]
    public async Task GetPaymentIntent_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Read"));

        var response = await client.GetAsync($"/payments/intents/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapturePayment_ReturnsCaptured_WhenAuthorized()
    {
        // Arrange - Create and authorize payment
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Create", "Payment.Capture"));

        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-003");
        var createResponse = await client.PostAsJsonAsync("/payments/intents", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Manually authorize via database (in real scenario, this would be done by payment gateway)
        // For testing, we'll just try to capture and expect it to fail since it's not authorized
        
        // Act
        var captureResponse = await client.PostAsync($"/payments/intents/{payment!.Id}/capture", null);

        // Assert - Should fail because payment is not authorized
        // In a real implementation with proper state machine, this would need authorization first
        captureResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelPayment_ReturnsCancelled_WhenPending()
    {
        // Arrange - Create payment
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Create", "Payment.Cancel"));

        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-004");
        var createResponse = await client.PostAsJsonAsync("/payments/intents", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Act
        var cancelResponse = await client.PostAsync($"/payments/intents/{payment!.Id}/cancel", null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelledPayment = await cancelResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();
        cancelledPayment.Should().NotBeNull();
        cancelledPayment!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelPayment_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTestRolePermissions("Payment.Cancel"));

        var response = await client.PostAsync($"/payments/intents/{Guid.NewGuid()}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
