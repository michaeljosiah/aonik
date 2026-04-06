using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class FileSessionStoreTests
{
    [Fact]
    public async Task SaveAsync_AndLoadAsync_ShouldRoundTripSession()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"AonikCliTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var sessionPath = Path.Combine(tempDirectory, "session.json");
        var store = new FileSessionStore(sessionPath);
        var session = new CliSession(
            "https://api.aonik.local",
            "token-123",
            "refresh-123",
            DateTimeOffset.Parse("2026-04-06T12:00:00Z"),
            "Auth0",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "operator@aonik.io",
            "session-1",
            "thread-1");

        try
        {
            // Act
            await store.SaveAsync(session);
            var loaded = await store.LoadAsync();

            // Assert
            loaded.Should().BeEquivalentTo(session);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
