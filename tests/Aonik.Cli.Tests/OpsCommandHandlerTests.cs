using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class OpsCommandHandlerTests
{
    [Fact]
    public async Task RunWorkflowAsync_ShouldWriteWorkflowResponse()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.RunWorkflowAsync(
            new RunWorkflowOptions("reconciliation", "Review settlements", OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("Workflow complete");
    }

    [Fact]
    public async Task TriggerJobAsync_ShouldWriteQueuedAction()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.TriggerJobAsync(
            new JobTriggerOptions("daily-reconciliation", OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("Queued");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ShouldWritePaymentIntent()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.CreatePaymentIntentAsync(
            new CreatePaymentIntentOptions(
                100m,
                "USD",
                "PAY-1001",
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                null,
                OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("PAY-1001");
    }

    private static OpsCommandHandler CreateHandler(out StringWriter writer)
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
        return new OpsCommandHandler(apiClient, sessionStore, outputWriter);
    }
}
