using System.Net;
using System.Net.Http.Json;
using Aonik.Api.Contracts.Ai;
using Aonik.Api.Contracts.Billing;
using FluentAssertions;

namespace Aonik.Api.Tests;

public class AiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateInvoiceInsight_ReturnsInsight_WhenInvoiceExists()
    {
        // Arrange - Create an invoice first
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

        var invoiceResponse = await _client.PostAsJsonAsync("/billing/invoices", invoiceRequest);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        var response = await _client.PostAsync($"/ai/invoices/{invoice!.Id}/insight", null);

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
        var response = await _client.PostAsync($"/ai/invoices/{Guid.NewGuid()}/insight", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
