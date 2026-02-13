using Aonik.Infrastructure.Caching;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Caching;

public class CacheSetRegistryTests
{
    [Fact]
    public void Track_ShouldAddKeyToSet()
    {
        // Arrange
        var registry = new CacheSetRegistry();

        // Act
        registry.Track("settings", "settings:global:key");

        // Assert
        registry.GetKeys("settings").Should().ContainSingle().Which.Should().Be("settings:global:key");
    }

    [Fact]
    public void RemoveKey_ShouldRemoveKeyFromSet()
    {
        // Arrange
        var registry = new CacheSetRegistry();
        registry.Track("settings", "settings:global:key");

        // Act
        registry.RemoveKey("settings", "settings:global:key");

        // Assert
        registry.GetKeys("settings").Should().BeEmpty();
    }
}
