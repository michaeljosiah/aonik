using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Api.Contracts.Ai;
using Aonik.Api.Contracts.Billing;

namespace Aonik.Api.Tests;

public class AiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenerateInvoiceInsight_ReturnsInsight_WhenInvoiceExists()
    {
        // Arrange - Create an invoice first
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Invoice.Create", "Invoice.Read"));


        var invoiceRequest = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: $"INV-{Guid.NewGuid().ToString()[..8]}",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Consulting Services", 10, 150.00m),
                new("Software License", 1, 500.00m)
            });

        var invoiceResponse = await client.PostAsJsonAsync("/billing/invoices", invoiceRequest);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        var response = await client.PostAsync($"/ai/invoices/{invoice!.Id}/insight", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var insight = await response.Content.ReadFromJsonAsync<InsightResponse>();
        insight.Should().NotBeNull();
        insight!.SubjectType.Should().Be("Invoice");
        insight.SubjectId.Should().Be(invoice.Id);
        insight.Title.Should().NotBeNullOrEmpty();
        insight.Summary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateInvoiceInsight_ReturnsNotFound_WhenInvoiceDoesNotExist()
    {
        // Act
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithPermissions("Invoice.Read"));


        var response = await client.PostAsync($"/ai/invoices/{Guid.NewGuid()}/insight", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
