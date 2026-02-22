using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Domain.Features.Entities;
using Aonik.Infrastructure.Features;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Infrastructure.Tests.Features;

public class DatabaseFeatureManagerTests
{
    private const string FeatureName = "BillPayments.Invoicing.Create";

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private sealed class InMemoryFeatureManagerSnapshot : IFeatureManagerSnapshot
    {
        private readonly Dictionary<string, bool> _flags;

        public InMemoryFeatureManagerSnapshot(Dictionary<string, bool> flags)
        {
            _flags = flags;
        }

        public Task<bool> IsEnabledAsync(string feature)
        {
            return Task.FromResult(_flags.TryGetValue(feature, out var enabled) && enabled);
        }

        public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
        {
            return IsEnabledAsync(feature);
        }

        public async IAsyncEnumerable<string> GetFeatureNamesAsync()
        {
            foreach (var key in _flags.Keys)
            {
                yield return key;
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnTenantOverride_WhenOverrideExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId), clock: clock);
        context.TenantFeatures.Add(new TenantFeature
        {
            TenantId = tenantId,
            FeatureName = FeatureName,
            IsEnabled = false
        });
        await context.SaveChangesAsync();

        var snapshot = new InMemoryFeatureManagerSnapshot(new Dictionary<string, bool>
        {
            [FeatureName] = true
        });

        var manager = new DatabaseFeatureManager(snapshot, context, new TestTenantProvider(tenantId), clock);

        // Act
        var isEnabled = await manager.IsEnabledAsync(FeatureName);

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldFallBackToConfig_WhenNoOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId), clock: clock);

        var snapshot = new InMemoryFeatureManagerSnapshot(new Dictionary<string, bool>
        {
            [FeatureName] = true
        });

        var manager = new DatabaseFeatureManager(snapshot, context, new TestTenantProvider(tenantId), clock);

        // Act
        var isEnabled = await manager.IsEnabledAsync(FeatureName);

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldIgnoreExpiredOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId), clock: clock);
        context.TenantFeatures.Add(new TenantFeature
        {
            TenantId = tenantId,
            FeatureName = FeatureName,
            IsEnabled = false,
            ExpiresAt = clock.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        var snapshot = new InMemoryFeatureManagerSnapshot(new Dictionary<string, bool>
        {
            [FeatureName] = true
        });

        var manager = new DatabaseFeatureManager(snapshot, context, new TestTenantProvider(tenantId), clock);

        // Act
        var isEnabled = await manager.IsEnabledAsync(FeatureName);

        // Assert
        isEnabled.Should().BeTrue();
    }
}
