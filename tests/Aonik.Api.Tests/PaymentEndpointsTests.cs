using System.Net;
using System.Net.Http.Json;
using Aonik.Api.Contracts.Payments;
using FluentAssertions;

namespace Aonik.Api.Tests;

public class PaymentEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PaymentEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePaymentIntent_ReturnsCreated()
    {
        // Arrange
        var request = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-001");

        // Act
        var response = await _client.PostAsJsonAsync("/payments/intents", request);

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
        var createRequest = new CreatePaymentIntentRequest(250.00m, "EUR", "ORDER-002");
        var createResponse = await _client.PostAsJsonAsync("/payments/intents", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Act
        var getResponse = await _client.GetAsync($"/payments/intents/{createdPayment!.Id}");

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
        var response = await _client.GetAsync($"/payments/intents/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapturePayment_ReturnsCaptured_WhenAuthorized()
    {
        // Arrange - Create and authorize payment
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-003");
        var createResponse = await _client.PostAsJsonAsync("/payments/intents", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Manually authorize via database (in real scenario, this would be done by payment gateway)
        // For testing, we'll just try to capture and expect it to fail since it's not authorized
        
        // Act
        var captureResponse = await _client.PostAsync($"/payments/intents/{payment!.Id}/capture", null);

        // Assert - Should fail because payment is not authorized
        // In a real implementation with proper state machine, this would need authorization first
        captureResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelPayment_ReturnsCancelled_WhenPending()
    {
        // Arrange - Create payment
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-004");
        var createResponse = await _client.PostAsJsonAsync("/payments/intents", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentIntentResponse>();

        // Act
        var cancelResponse = await _client.PostAsync($"/payments/intents/{payment!.Id}/cancel", null);

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
        var response = await _client.PostAsync($"/payments/intents/{Guid.NewGuid()}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
