using Aonik.Infrastructure.Caching;
using Aonik.SharedKernel.Caching;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Caching;

public class CachePolicyProviderTests
{
    [Fact]
    public void Get_ShouldReturnShortPolicy_WhenShortRequested()
    {
        // Arrange
        var provider = new CachePolicyProvider();

        // Act
        var options = provider.Get(CachePolicy.Short);

        // Assert
        options.Duration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Get_ShouldReturnMediumPolicy_WhenMediumRequested()
    {
        // Arrange
        var provider = new CachePolicyProvider();

        // Act
        var options = provider.Get(CachePolicy.Medium);

        // Assert
        options.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Get_ShouldReturnLongPolicy_WhenLongRequested()
    {
        // Arrange
        var provider = new CachePolicyProvider();

        // Act
        var options = provider.Get(CachePolicy.Long);

        // Assert
        options.Duration.Should().Be(TimeSpan.FromMinutes(30));
    }
}
