using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;
using FluentAssertions;

namespace Aonik.Cli.Tests;

public sealed class AuthCommandHandlerTests
{
    [Fact]
    public async Task LoginAsync_ShouldPersistSessionAndWriteSummary_WhenAccessTokenProvided()
    {
        // Arrange
        var apiClient = new FakeAonikCliApiClient();
        var sessionStore = new InMemorySessionStore();
        var writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        var handler = new AuthCommandHandler(apiClient, sessionStore, outputWriter);

        // Act
        var exitCode = await handler.LoginAsync(
            new LoginOptions(
                BaseUrl: "https://api.aonik.local",
                Username: null,
                Password: null,
                AccessToken: "token-123",
                ClientId: null,
                Scope: null,
                TenantId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                OutputMode: OutputMode.Text));

        // Assert
        exitCode.Should().Be(0);
        sessionStore.Session.Should().NotBeNull();
        sessionStore.Session!.AccessToken.Should().Be("token-123");
        sessionStore.Session.Email.Should().Be("operator@aonik.io");
        writer.ToString().Should().Contain("AONIK session: https://api.aonik.local");
        writer.ToString().Should().Contain("operator@aonik.io");
    }

    [Fact]
    public async Task WhoAmIAsync_ShouldRefreshSessionFromApi()
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
                null,
                null,
                null,
                null,
                null));

        var writer = new StringWriter();
        var outputWriter = new TextWriterCliOutputWriter(writer);
        var handler = new AuthCommandHandler(apiClient, sessionStore, outputWriter);

        // Act
        var exitCode = await handler.WhoAmIAsync(OutputMode.Text);

        // Assert
        exitCode.Should().Be(0);
        sessionStore.Session!.UserId.Should().Be(apiClient.UserInfoResponse.UserId);
        sessionStore.Session.TenantId.Should().Be(apiClient.UserInfoResponse.TenantId);
        writer.ToString().Should().Contain("Authenticated user: operator@aonik.io");
    }
}
