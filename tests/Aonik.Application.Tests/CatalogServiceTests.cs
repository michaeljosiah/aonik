using System.Text.Json;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Catalog;
using Aonik.Application.Services.Catalog;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.ReferenceData.Entities;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.ReferenceData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Aonik.Application.Tests;

public class CatalogServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class AllowAllPermissionService : Aonik.Application.Services.Identity.IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : Aonik.SharedKernel.Abstractions.ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId) => _userId = userId;

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    [Fact]
    public async Task GetCountriesAsync_ShouldFilterToServiceCountries_WhenRequested()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        context.ReferenceDataItems.AddRange(
            new ReferenceDataItem
            {
                Id = Guid.NewGuid(),
                Type = "Country",
                Code = "GH",
                DisplayName = "Ghana",
                SortOrder = 1,
                IsActive = true
            },
            new ReferenceDataItem
            {
                Id = Guid.NewGuid(),
                Type = "Country",
                Code = "KE",
                DisplayName = "Kenya",
                SortOrder = 2,
                IsActive = true
            });

        context.CatalogBillers.Add(new CatalogBiller
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = Guid.NewGuid(),
            CountryCode = "GH",
            Name = "ECG",
            IsActive = true,
            SortOrder = 1
        });

        await context.SaveChangesAsync();

        var referenceDataService = new ReferenceDataService(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            new TestTenantProvider(tenantId));

        var service = new CatalogService(
            context,
            referenceDataService,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()));

        // Act
        var result = await service.GetCountriesAsync(new CatalogCountryListRequest(true), CancellationToken.None);

        // Assert
        result.Countries.Should().ContainSingle();
        result.Countries[0].CountryCode.Should().Be("GH");
    }

    [Fact]
    public async Task GetBillersAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));

        var categoryId = Guid.NewGuid();
        var correspondentId = Guid.NewGuid();
        context.CatalogBillers.AddRange(
            new CatalogBiller
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CategoryId = categoryId,
                CorrespondentPartnerId = correspondentId,
                CountryCode = "GH",
                Name = "ECG",
                IsActive = true,
                SortOrder = 1
            },
            new CatalogBiller
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CategoryId = categoryId,
                CountryCode = "GH",
                Name = "Ghana Water",
                IsActive = true,
                SortOrder = 2
            });

        await context.SaveChangesAsync();

        var referenceDataService = new ReferenceDataService(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            new TestTenantProvider(tenantId));

        var service = new CatalogService(
            context,
            referenceDataService,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()));

        // Act
        var result = await service.GetBillersAsync(
            new CatalogBillerListRequest("GH", categoryId, null, 1, 1),
            CancellationToken.None);

        // Assert
        result.Billers.Should().HaveCount(1);
        result.Pagination.TotalCount.Should().Be(2);
        result.Pagination.TotalPages.Should().Be(2);
        result.Billers[0].CorrespondentPartnerId.Should().Be(correspondentId);
    }

    [Fact]
    public async Task GetBillerServiceDetailAsync_ShouldDeserializeFields()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        var billerId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var fields = new List<CatalogServiceField>
        {
            new(
                "meterNumber",
                "Meter Number",
                "text",
                true,
                6,
                12,
                "############",
                "Enter meter number",
                null)
        };

        context.CatalogBillerServices.Add(new CatalogBillerService
        {
            Id = serviceId,
            TenantId = tenantId,
            BillerId = billerId,
            ServiceCode = "ELECTRICITY_PREPAID",
            Name = "Prepaid",
            Type = "prepaid",
            Currency = "GHS",
            SupportsPartialPayment = false,
            RequiresValidation = true,
            IsActive = true,
            SortOrder = 1,
            FieldsJson = JsonSerializer.Serialize(fields)
        });

        await context.SaveChangesAsync();

        var referenceDataService = new ReferenceDataService(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            new TestTenantProvider(tenantId));

        var service = new CatalogService(
            context,
            referenceDataService,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()));

        // Act
        var result = await service.GetBillerServiceDetailAsync(billerId, serviceId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Fields.Should().HaveCount(1);
        result.Fields[0].Key.Should().Be("meterNumber");
    }
}
