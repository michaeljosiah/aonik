using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class AgentCommandHandlerTests
{
    [Fact]
    public async Task RunAsync_ShouldPersistConversationIdsAndWriteResponse()
    {
        // Arrange
        var apiClient = new FakeAonikCliApiClient();
        var sessionStore = new InMemorySessionStore();
        await sessionStore.SaveAsync(
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
                null));

        var writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        var handler = new AgentCommandHandler(apiClient, sessionStore, outputWriter);

        // Act
        var exitCode = await handler.RunAsync(
            new RunAgentOptions(
                Message: "Reconcile today's settlements",
                SessionId: null,
                ThreadId: null,
                OutputMode: OutputMode.Text));

        // Assert
        exitCode.Should().Be(0);
        sessionStore.Session!.LastSessionId.Should().Be("session-123");
        sessionStore.Session.LastThreadId.Should().Be("thread-123");
        writer.ToString().Should().Contain("Here is the response.");
        writer.ToString().Should().Contain("finance-agent");
    }

    [Fact]
    public async Task ListAsync_ShouldWriteJson_WhenJsonOutputRequested()
    {
        // Arrange
        var apiClient = new FakeAonikCliApiClient();
        var sessionStore = new InMemorySessionStore();
        await sessionStore.SaveAsync(
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
                null));

        var writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        var handler = new AgentCommandHandler(apiClient, sessionStore, outputWriter);

        // Act
        var exitCode = await handler.ListAsync(OutputMode.Json);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("finance-agent");
        writer.ToString().Should().Contain("platform-agent");
        writer.ToString().Should().Contain("[");
    }

    [Fact]
    public async Task StreamAsync_ShouldPersistStreamingConversationIds()
    {
        // Arrange
        var apiClient = new FakeAonikCliApiClient();
        var sessionStore = new InMemorySessionStore();
        await sessionStore.SaveAsync(
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
                null));

        var writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        var handler = new AgentCommandHandler(apiClient, sessionStore, outputWriter);

        // Act
        var exitCode = await handler.StreamAsync(
            new StreamAgentOptions(
                Message: "Stream this",
                ThreadId: null,
                RunId: null,
                AgentId: null,
                OutputMode: OutputMode.Ndjson));

        // Assert
        exitCode.Should().Be(0);
        sessionStore.Session!.LastThreadId.Should().Be("thread-stream");
        sessionStore.Session.LastSessionId.Should().Be("run-stream");
        writer.ToString().Should().Contain("TEXT_MESSAGE_CONTENT");
    }
}
