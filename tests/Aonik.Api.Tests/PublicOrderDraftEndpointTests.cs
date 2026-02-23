using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Entities.Identity;
using Aonik.Finance.Persistence;
using Aonik.Platform.Persistence;

namespace Aonik.Api.Tests;

public class PublicOrderDraftEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicOrderDraftEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePublicOrderDraft_ShouldPersistDraftOrder_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var request = new PublicOrderDraftRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BILLPAY.ELECTRICITY.PREPAID.GH.ECG",
            "ECG Prepaid Electricity",
            "ECG Power",
            "GH",
            "GHS",
            new Dictionary<string, string>
            {
                ["meterNumber"] = "1234567890"
            },
            true,
            DateTimeOffset.UtcNow,
            "precheck",
            null,
            50,
            "Payabo");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.PostAsJsonAsync("/public/orders/bill-payments/drafts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicOrderDraftResponse>();
        payload.Should().NotBeNull();
        payload!.OrderId.Should().NotBe(Guid.Empty);
        payload.Status.Should().Be("Draft");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var order = await dbContext.Orders.FirstOrDefaultAsync(orderEntity => orderEntity.Id == payload.OrderId);
        order.Should().NotBeNull();
        order!.Status.Should().Be("Draft");
        order.OrderType.Should().Be("BillPayment");
        order.OriginCountry.Should().Be("GH");
        order.CurrencyIn.Should().Be("GHS");
        order.ProvenanceJson.Should().Contain("GuestDraft");
    }

    [Fact]
    public async Task GetPublicOrderDraft_ShouldReturnDraftDetails_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var createRequest = new PublicOrderDraftRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BILLPAY.WATER.POSTPAID.GH.GWL",
            "Ghana Water Postpaid",
            "Ghana Water",
            "GH",
            "GHS",
            new Dictionary<string, string>
            {
                ["accountNumber"] = "12345678"
            },
            true,
            DateTimeOffset.UtcNow,
            "precheck",
            null,
            80,
            "Payabo");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createResponse = await client.PostAsJsonAsync("/public/orders/bill-payments/drafts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PublicOrderDraftResponse>();
        created.Should().NotBeNull();

        // Act
        var getResponse = await client.GetAsync($"/public/orders/bill-payments/drafts/{created!.OrderId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await getResponse.Content.ReadFromJsonAsync<PublicOrderDraftDetailResponse>();
        payload.Should().NotBeNull();
        payload!.OrderId.Should().Be(created.OrderId);
        payload.ServiceCode.Should().Be("BILLPAY.WATER.POSTPAID.GH.GWL");
        payload.ServiceFieldValues.Should().ContainKey("accountNumber");
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == tenantId);
        if (existingTenant != null)
        {
            return;
        }

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Public Order Draft Tenant",
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

    private sealed record PublicOrderDraftDetailResponse(
        Guid OrderId,
        string Status,
        DateTime CreatedAt,
        string CountryCode,
        string Currency,
        Guid BillerId,
        string? BillerName,
        Guid ServiceId,
        string ServiceCode,
        string ServiceName,
        Dictionary<string, string> ServiceFieldValues,
        bool IsValidated,
        DateTimeOffset CapturedAt,
        string? ValidationMode,
        string? AccountHolderName,
        decimal? RequestedAmount,
        string Channel);
}
