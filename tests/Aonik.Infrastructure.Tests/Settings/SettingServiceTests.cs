using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Caching;

namespace Aonik.Infrastructure.Tests.Settings;

public class SettingServiceTests
{
    private sealed class PassthroughCacheStore : ICacheStore
    {
        public Task<T?> GetOrSetAsync<T>(
            string key,
            CachePolicy policy,
            Func<CancellationToken, Task<T?>> factory,
            string cacheSet,
            CancellationToken cancellationToken = default)
        {
            return factory(cancellationToken);
        }
    }

    private sealed class PassthroughSettingValueProtector : ISettingValueProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => Guid.Empty;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = Guid.Empty;
            return false;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => null;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = Guid.Empty;
            return false;
        }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
        {
            return Task.FromResult(new List<string>());
        }
    }

    private static AonikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AonikDbContext(options);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static SettingService CreateService(AonikDbContext dbContext, IConfiguration configuration)
    {
        return new SettingService(
            dbContext,
            configuration,
            new PassthroughCacheStore(),
            new PassthroughSettingValueProtector(),
            new TestTenantProvider(),
            new TestCurrentUserProvider(),
            new AllowAllPermissionService(),
            new CacheInvalidationPublisher());
    }

    [Fact]
    public async Task GetAsync_Should_ReturnConfigurationValue_When_GlobalSettingMissingInDatabase()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Settings:Auth.AzureAd.TenantId"] = "tenant-from-config"
        });
        var service = CreateService(dbContext, configuration);

        // Act
        var value = await service.GetAsync(AuthSettingNames.AzureAdTenantId);

        // Assert
        value.Should().Be("tenant-from-config");
    }

    [Fact]
    public async Task GetAsync_Should_ReturnDefaultValue_When_DatabaseAndConfigurationAreMissing()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var service = CreateService(dbContext, configuration);

        // Act
        var value = await service.GetAsync(AuthSettingNames.Provider);

        // Assert
        value.Should().Be("AzureAd");
    }

    [Fact]
    public async Task GetAsync_Should_PreferDatabaseValue_OverConfigurationValue()
    {
        const string settingKey = "Platform.Tests.SampleKey";

        // Arrange
        using var dbContext = CreateDbContext();
        dbContext.Settings.Add(new Setting
        {
            Key = settingKey,
            Value = "value-from-db",
            Scope = SettingScope.Global,
        });
        await dbContext.SaveChangesAsync();

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Settings:Platform.Tests.SampleKey"] = "value-from-config"
        });
        var service = CreateService(dbContext, configuration);

        // Act
        var value = await service.GetAsync(settingKey);

        // Assert
        value.Should().Be("value-from-db");
    }

    [Fact]
    public async Task GetAsync_Should_PreferConfigurationValue_ForAuthKeys_WhenDatabaseValueExists()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        dbContext.Settings.Add(new Setting
        {
            Key = AuthSettingNames.AzureAdTenantId,
            Value = "tenant-from-db",
            Scope = SettingScope.Global,
        });
        await dbContext.SaveChangesAsync();

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Settings:Auth.AzureAd.TenantId"] = "tenant-from-config"
        });
        var service = CreateService(dbContext, configuration);

        // Act
        var value = await service.GetAsync(AuthSettingNames.AzureAdTenantId);

        // Assert
        value.Should().Be("tenant-from-config");
    }

    [Fact]
    public async Task GetRequiredAsync_Should_UseConfigurationFallback_When_GlobalSettingMissingInDatabase()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Settings:Auth.AzureAd.TenantId"] = "tenant-from-config"
        });
        var service = CreateService(dbContext, configuration);

        // Act
        var value = await service.GetRequiredAsync(AuthSettingNames.AzureAdTenantId);

        // Assert
        value.Should().Be("tenant-from-config");
    }
}
