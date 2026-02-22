using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Infrastructure.Features;

namespace Aonik.Infrastructure.Tests.Features;

public class TenantFeatureFilterTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid? _tenantId;

        public TestTenantProvider(Guid? tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId()
        {
            if (_tenantId == null)
            {
                throw new InvalidOperationException("Tenant context not available");
            }

            return _tenantId.Value;
        }

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            if (_tenantId.HasValue)
            {
                tenantId = _tenantId.Value;
                return true;
            }

            tenantId = Guid.Empty;
            return false;
        }
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnFalse_WhenTenantNotResolved()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider(tenantId: null);
        var filter = new TenantFeatureFilter(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var context = new FeatureFilterEvaluationContext
        {
            Parameters = BuildParameters(new Dictionary<string, string?>
            {
                ["AllowedTenants:0"] = "*"
            })
        };

        // Act
        var result = await filter.EvaluateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnTrue_WhenWildcardIsConfigured()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var serviceProvider = BuildServiceProvider(tenantId);
        var filter = new TenantFeatureFilter(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var context = new FeatureFilterEvaluationContext
        {
            Parameters = BuildParameters(new Dictionary<string, string?>
            {
                ["AllowedTenants:0"] = "*"
            })
        };

        // Act
        var result = await filter.EvaluateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnTrue_WhenTenantIsAllowed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var serviceProvider = BuildServiceProvider(tenantId);
        var filter = new TenantFeatureFilter(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var context = new FeatureFilterEvaluationContext
        {
            Parameters = BuildParameters(new Dictionary<string, string?>
            {
                ["AllowedTenants:0"] = tenantId.ToString()
            })
        };

        // Act
        var result = await filter.EvaluateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnFalse_WhenTenantIsNotAllowed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var serviceProvider = BuildServiceProvider(tenantId);
        var filter = new TenantFeatureFilter(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var context = new FeatureFilterEvaluationContext
        {
            Parameters = BuildParameters(new Dictionary<string, string?>
            {
                ["AllowedTenants:0"] = Guid.NewGuid().ToString()
            })
        };

        // Act
        var result = await filter.EvaluateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    private static IConfiguration BuildParameters(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider BuildServiceProvider(Guid? tenantId)
    {
        return new ServiceCollection()
            .AddScoped<ITenantProvider>(_ => new TestTenantProvider(tenantId))
            .BuildServiceProvider();
    }
}
