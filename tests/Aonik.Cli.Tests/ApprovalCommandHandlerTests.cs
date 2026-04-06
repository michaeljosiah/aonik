using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class ApprovalCommandHandlerTests
{
    [Fact]
    public async Task ListAsync_ShouldWritePendingProposals()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.ListAsync(OutputMode.Json);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("Netflix");
    }

    [Fact]
    public async Task ApproveAsync_ShouldWriteApprovedStatus()
    {
        // Arrange
        var handler = CreateHandler(out var writer);
        var proposalId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        // Act
        var exitCode = await handler.ApproveAsync(proposalId, OutputMode.Json);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("approved");
    }

    private static ApprovalCommandHandler CreateHandler(out StringWriter writer)
    {
        var apiClient = new FakeAonikCliApiClient();
        var sessionStore = new InMemorySessionStore();
        sessionStore.SaveAsync(
            new CliSession(
                "https://api.aonik.local",
                "token-123",
                null,
                null,
                "Auth0",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "operator@aonik.io",
                null,
                null)).GetAwaiter().GetResult();

        writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        return new ApprovalCommandHandler(apiClient, sessionStore, outputWriter);
    }
}
