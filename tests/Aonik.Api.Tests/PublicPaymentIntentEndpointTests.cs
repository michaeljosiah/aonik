using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Api.Tests;

public class PublicPaymentIntentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicPaymentIntentEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePublicPaymentIntent_ShouldCreateIntent_AndMoveOrderToPendingFunding()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var draftResponse = await client.PostAsJsonAsync(
            "/public/orders/bill-payments/drafts",
            new PublicOrderDraftRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "BILLPAY.ELECTRICITY.PREPAID.GH.ECG",
                "ECG Prepaid Electricity",
                "ECG",
                "GH",
                "GHS",
                new Dictionary<string, string> { ["meterNumber"] = "1234567890" },
                true,
                DateTimeOffset.UtcNow,
                "precheck",
                null,
                120,
                "Payabo"));

        var draft = await draftResponse.Content.ReadFromJsonAsync<PublicOrderDraftResponse>();
        draft.Should().NotBeNull();

        // Act
        var response = await client.PostAsJsonAsync(
            "/public/payments/intents",
            new PublicPaymentIntentRequest(
                draft!.OrderId,
                "Stripe",
                "Card",
                "https://localhost/payments/status-payment-sent",
                "https://localhost/payments/selection"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PublicPaymentIntentResponse>();
        payload.Should().NotBeNull();
        payload!.OrderId.Should().Be(draft.OrderId);
        payload.Provider.Should().Be("Stripe");
        payload.ProviderReference.Should().StartWith("pi_");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var order = await dbContext.Orders.FirstOrDefaultAsync(entity => entity.Id == draft.OrderId);
        order.Should().NotBeNull();
        order!.Status.Should().Be("PendingFunding");

        var paymentIntent = await dbContext.PaymentIntents.FirstOrDefaultAsync(entity => entity.Id == payload.PaymentIntentId);
        paymentIntent.Should().NotBeNull();
        paymentIntent!.OrderId.Should().Be(draft.OrderId);
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == tenantId);
        if (existingTenant != null)
        {
            return;
        }

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Public Payment Intent Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = "Active"
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed record PublicOrderDraftRequest(
        Guid BillerId,
        Guid ServiceId,
        string ServiceCode,
        string ServiceName,
        string? BillerName,
        string CountryCode,
        string Currency,
        Dictionary<string, string> ServiceFieldValues,
        bool IsValidated,
        DateTimeOffset CapturedAt,
        string? ValidationMode,
        string? AccountHolderName,
        decimal? RequestedAmount,
        string? Channel);

    private sealed record PublicOrderDraftResponse(Guid OrderId, string Status, DateTime CreatedAt);

    private sealed record PublicPaymentIntentRequest(
        Guid OrderId,
        string Provider,
        string PaymentMethodType,
        string? ReturnUrl,
        string? CancelUrl);

    private sealed record PublicPaymentIntentResponse(
        Guid PaymentIntentId,
        Guid OrderId,
        decimal Amount,
        string Currency,
        string Status,
        string Provider,
        string ProviderReference,
        string? ClientSecret,
        string? CheckoutUrl,
        DateTime CreatedAt);
}
