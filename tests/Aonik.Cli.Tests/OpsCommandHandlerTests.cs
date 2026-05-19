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

    [Fact]
    public async Task IssueInvoiceAsync_ShouldRefuse_WithoutConfirm()
    {
        // Arrange
        var handler = CreateHandler(out _);

        // Act
        var act = async () => await handler.IssueInvoiceAsync(
            new InvoiceMutationOptions(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Confirm: false,
                OutputMode.Json));

        // Assert
        await act.Should().ThrowAsync<AonikCliException>()
            .WithMessage("*--confirm*");
    }

    [Fact]
    public async Task IssueInvoiceAsync_ShouldTransitionToIssued_WithConfirm()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.IssueInvoiceAsync(
            new InvoiceMutationOptions(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Confirm: true,
                OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("Issued");
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldWriteCreatedInvoice()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.CreateInvoiceAsync(
            new CreateInvoiceOptions(
                CustomerId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                InvoiceNumber: "INV-1001",
                Currency: "USD",
                DueUtc: DateTime.Parse("2026-04-20T08:00:00Z").ToUniversalTime(),
                LinesFile: null,
                OutputMode: OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("INV-1001");
    }

    [Fact]
    public async Task ListOrdersAsync_ShouldWritePagedResponse()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.ListOrdersAsync(
            new ListOrdersOptions(
                Page: 1,
                PageSize: 20,
                Status: null,
                OrderType: null,
                Search: null,
                PayerPartyId: null,
                OutputMode: OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("Acme Co");
    }

    [Fact]
    public async Task SubmitOrderAsync_ShouldRefuse_WithoutConfirm()
    {
        // Arrange
        var handler = CreateHandler(out _);

        // Act
        var act = async () => await handler.SubmitOrderAsync(
            new SubmitOrderOptions(
                Guid.Parse("aaaa1111-1111-1111-1111-111111111111"),
                Confirm: false,
                OutputMode.Json));

        // Assert
        await act.Should().ThrowAsync<AonikCliException>()
            .WithMessage("*--confirm*");
    }

    [Fact]
    public async Task PauseScheduledJobAsync_ShouldWritePauseAction()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.PauseScheduledJobAsync(
            "daily-reconciliation",
            OutputMode.Json);

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("pause");
    }

    [Fact]
    public async Task ListScheduledJobRunsAsync_ShouldWriteRunHistory()
    {
        // Arrange
        var handler = CreateHandler(out var writer);

        // Act
        var exitCode = await handler.ListScheduledJobRunsAsync(
            new ListJobRunsOptions(
                JobName: "daily-reconciliation",
                Page: 1,
                PageSize: 20,
                OutputMode: OutputMode.Json));

        // Assert
        exitCode.Should().Be(0);
        writer.ToString().Should().Contain("scheduler");
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
