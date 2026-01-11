using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Api.Contracts.Billing;

namespace Aonik.Api.Tests;

public class InvoiceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InvoiceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _factory.CreateClient().GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateInvoice_ReturnsCreated()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Invoice.Create", "Invoice.Read"));

        var request = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: $"INV-{Guid.NewGuid().ToString()[..8]}",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Test Service", 1, 100.00m)
            });

        // Act
        var response = await client.PostAsJsonAsync("/billing/invoices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.InvoiceNumber.Should().Be(request.InvoiceNumber);
        invoice.TotalAmount.Should().Be(100.00m);
    }

    [Fact]
    public async Task GetInvoice_ReturnsInvoice_WhenExists()
    {
        // Arrange - Create an invoice first
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Invoice.Create", "Invoice.Read"));

        var createRequest = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: $"INV-{Guid.NewGuid().ToString()[..8]}",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Test Service", 1, 100.00m)
            });

        var createResponse = await client.PostAsJsonAsync("/billing/invoices", createRequest);
        var createdInvoice = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        var getResponse = await client.GetAsync($"/billing/invoices/{createdInvoice!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoice = await getResponse.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.Id.Should().Be(createdInvoice.Id);
    }

    [Fact]
    public async Task GetInvoice_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Invoice.Read"));
        var response = await client.GetAsync($"/billing/invoices/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
