using Aonik.Cli;
using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_ShouldInvokeCommandTree_ForAgentList()
    {
        // Arrange
        var application = CreateApplication(out var writer);

        // Act
        var exitCode = await application.RunAsync(["agent", "list", "--output", "json"]);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("finance-agent");
        writer.ToString().Should().Contain("platform-agent");
    }

    [Fact]
    public async Task RunAsync_ShouldInvokeCommandTree_ForAgentStreamNdjson()
    {
        // Arrange
        var application = CreateApplication(out var writer);

        // Act
        var exitCode = await application.RunAsync(["agent", "stream", "--message", "hello", "--output", "ndjson"]);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("RUN_STARTED");
        writer.ToString().Should().Contain("TEXT_MESSAGE_CONTENT");
        writer.ToString().Should().Contain("RUN_FINISHED");
    }

    [Fact]
    public async Task RunAsync_ShouldInvokeCommandTree_ForOpsJobsList()
    {
        // Arrange
        var application = CreateApplication(out var writer);

        // Act
        var exitCode = await application.RunAsync(["ops", "jobs", "list", "--output", "json"]);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("daily-reconciliation");
    }

    private static CliApplication CreateApplication(out StringWriter writer)
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
        var authHandler = new AuthCommandHandler(apiClient, sessionStore, outputWriter);
        var agentHandler = new AgentCommandHandler(apiClient, sessionStore, outputWriter);
        var opsHandler = new OpsCommandHandler(apiClient, sessionStore, outputWriter);
        var approvalHandler = new ApprovalCommandHandler(apiClient, sessionStore, outputWriter);
        return new CliApplication(authHandler, agentHandler, opsHandler, approvalHandler);
    }
}
