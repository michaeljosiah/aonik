using System.Net;
using System.Net.Http.Json;
using Aonik.Api.Contracts.Billing;
using Aonik.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class InvoiceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvoiceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateInvoice_ReturnsCreated()
    {
        // Arrange
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
        var response = await _client.PostAsJsonAsync("/billing/invoices", request);

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
        var createRequest = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: $"INV-{Guid.NewGuid().ToString()[..8]}",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Test Service", 1, 100.00m)
            });

        var createResponse = await _client.PostAsJsonAsync("/billing/invoices", createRequest);
        var createdInvoice = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        var getResponse = await _client.GetAsync($"/billing/invoices/{createdInvoice!.Id}");

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
        var response = await _client.GetAsync($"/billing/invoices/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Provide a unique database name per test run
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid().ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            // Build the service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
