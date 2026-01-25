using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using Aonik.Application.Abstractions.Multitenancy;
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
        var filter = new TenantFeatureFilter(new TestTenantProvider(null));
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
        var filter = new TenantFeatureFilter(new TestTenantProvider(tenantId));
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
        var filter = new TenantFeatureFilter(new TestTenantProvider(tenantId));
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
        var filter = new TenantFeatureFilter(new TestTenantProvider(tenantId));
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
}
