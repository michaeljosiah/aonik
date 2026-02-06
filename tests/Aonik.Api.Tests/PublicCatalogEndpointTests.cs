using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Api.Tests;

public class PublicCatalogEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicCatalogEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPublicCatalogCountries_ShouldResolveTenantFromHeader_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/public/catalog/countries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPublicCatalogBillerCategories_ShouldReturnCategories_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var categoryId = Guid.NewGuid();
        await SeedCategoryAsync(tenantId, categoryId, "GH", "Utilities");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/public/catalog/billers/categories?countryCode=GH");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicBillerCategoryResponse>();
        payload.Should().NotBeNull();
        payload!.Categories.Should().ContainSingle();
        payload.Categories[0].CategoryId.Should().Be(categoryId);
        payload.Categories[0].Name.Should().Be("Utilities");
    }

    [Fact]
    public async Task GetPublicCatalogBillers_ShouldReturnSearchResults_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var categoryId = Guid.NewGuid();
        var correspondentPartnerId = Guid.NewGuid();

        await SeedCategoryAsync(tenantId, categoryId, "GH", "Utilities");
        await SeedBillerAsync(tenantId, categoryId, correspondentPartnerId, "GH", "ECG");
        await SeedBillerAsync(tenantId, categoryId, correspondentPartnerId, "GH", "Ghana Water");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/public/catalog/billers?countryCode=GH&search=ECG&page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicBillerResponse>();
        payload.Should().NotBeNull();
        payload!.Billers.Should().ContainSingle();
        payload.Billers[0].Name.Should().Be("ECG");
        payload.Pagination.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPublicCatalogBillerServices_ShouldReturnServices_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var categoryId = Guid.NewGuid();
        var billerId = Guid.NewGuid();
        var correspondentPartnerId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        await SeedCategoryAsync(tenantId, categoryId, "GH", "Utilities");
        await SeedBillerAsync(tenantId, categoryId, correspondentPartnerId, "GH", "ECG", billerId);
        await SeedServiceAsync(tenantId, billerId, serviceId, "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync($"/public/catalog/billers/{billerId}/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicBillerServicesResponse>();
        payload.Should().NotBeNull();
        payload!.Services.Should().ContainSingle();
        payload.Services[0].ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public async Task GetPublicCatalogBillerServiceDetail_ShouldReturnFieldSchema_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var categoryId = Guid.NewGuid();
        var billerId = Guid.NewGuid();
        var correspondentPartnerId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        const string fieldsJson = "[{\"Key\":\"meterNumber\",\"Label\":\"Meter number\",\"FieldType\":\"text\",\"Required\":true,\"MinLength\":6,\"MaxLength\":16,\"Mask\":null,\"Placeholder\":\"Enter meter number\",\"Options\":null}]";

        await SeedCategoryAsync(tenantId, categoryId, "GH", "Utilities");
        await SeedBillerAsync(tenantId, categoryId, correspondentPartnerId, "GH", "ECG", billerId);
        await SeedServiceAsync(tenantId, billerId, serviceId, "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid", fieldsJson: fieldsJson);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync($"/public/catalog/billers/{billerId}/services/{serviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicBillerServiceDetailResponse>();
        payload.Should().NotBeNull();
        payload!.ServiceId.Should().Be(serviceId);
        payload.Fields.Should().ContainSingle();
        payload.Fields[0].Key.Should().Be("meterNumber");
    }

    [Fact]
    public async Task ValidatePublicCatalogServiceFields_ShouldReturnInvalid_WhenRequiredFieldMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var categoryId = Guid.NewGuid();
        var billerId = Guid.NewGuid();
        var correspondentPartnerId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        const string fieldsJson = "[{\"Key\":\"meterNumber\",\"Label\":\"Meter number\",\"FieldType\":\"text\",\"Required\":true,\"MinLength\":6,\"MaxLength\":16,\"Mask\":null,\"Placeholder\":\"Enter meter number\",\"Options\":null}]";

        await SeedCategoryAsync(tenantId, categoryId, "GH", "Utilities");
        await SeedBillerAsync(tenantId, categoryId, correspondentPartnerId, "GH", "ECG", billerId);
        await SeedServiceAsync(tenantId, billerId, serviceId, "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid", fieldsJson: fieldsJson);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.PostAsJsonAsync(
            $"/public/catalog/billers/{billerId}/services/{serviceId}/validate",
            new PublicValidationRequest(new Dictionary<string, string>()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PublicValidationResponse>();
        payload.Should().NotBeNull();
        payload!.IsValid.Should().BeFalse();
        payload.ErrorCode.Should().Be("MISSING_REQUIRED_FIELD");
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (existingTenant != null)
        {
            return;
        }

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Payabo Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCategoryAsync(Guid tenantId, Guid categoryId, string countryCode, string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.TenantId = tenantId;

        var existingCategory = await dbContext.CatalogBillerCategories.FirstOrDefaultAsync(category => category.Id == categoryId);
        if (existingCategory != null)
        {
            return;
        }

        dbContext.CatalogBillerCategories.Add(new CatalogBillerCategory
        {
            Id = categoryId,
            TenantId = tenantId,
            CountryCode = countryCode,
            Name = name,
            SortOrder = 1,
            IsActive = true
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedBillerAsync(
        Guid tenantId,
        Guid categoryId,
        Guid correspondentPartnerId,
        string countryCode,
        string name,
        Guid? billerId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.TenantId = tenantId;

        var existingBiller = await dbContext.CatalogBillers
            .FirstOrDefaultAsync(biller => biller.TenantId == tenantId && biller.Name == name);
        if (existingBiller != null)
        {
            return;
        }

        dbContext.CatalogBillers.Add(new CatalogBiller
        {
            Id = billerId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            CorrespondentPartnerId = correspondentPartnerId,
            CountryCode = countryCode,
            Name = name,
            IsActive = true,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedServiceAsync(
        Guid tenantId,
        Guid billerId,
        Guid serviceId,
        string serviceCode,
        string name,
        string? fieldsJson = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.TenantId = tenantId;

        var existingService = await dbContext.CatalogBillerServices
            .FirstOrDefaultAsync(service => service.Id == serviceId);
        if (existingService != null)
        {
            return;
        }

        dbContext.CatalogBillerServices.Add(new CatalogBillerService
        {
            Id = serviceId,
            TenantId = tenantId,
            BillerId = billerId,
            ServiceCode = serviceCode,
            Name = name,
            Type = "Prepaid",
            Currency = "GHS",
            MinAmount = 5,
            MaxAmount = 500,
            SupportsPartialPayment = true,
            RequiresValidation = true,
            IsActive = true,
            SortOrder = 1,
            FieldsJson = fieldsJson ?? "[]"
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed record PublicBillerCategoryResponse(List<PublicBillerCategoryItem> Categories);

    private sealed record PublicBillerCategoryItem(Guid CategoryId, string Name);

    private sealed record PublicBillerResponse(List<PublicBillerItem> Billers, PublicPaginationResponse Pagination);

    private sealed record PublicBillerItem(Guid BillerId, string Name);

    private sealed record PublicPaginationResponse(int Page, int PageSize, int TotalCount, int TotalPages);

    private sealed record PublicBillerServicesResponse(List<PublicBillerServiceItem> Services);

    private sealed record PublicBillerServiceItem(Guid ServiceId, string Name);

    private sealed record PublicBillerServiceDetailResponse(Guid ServiceId, List<PublicServiceField> Fields);

    private sealed record PublicServiceField(string Key, string Label);

    private sealed record PublicValidationRequest(Dictionary<string, string> FieldValues);

    private sealed record PublicValidationResponse(bool IsValid, string? ErrorCode, string? ErrorMessage);
}
