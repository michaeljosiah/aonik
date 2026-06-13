using Aonik.Cli;
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

    // ── Dev-environment regression: the public auth-provider settings endpoint is
    // gated behind tenant resolution in deployed environments and 401s for the
    // tenant-less CLI. That discovery call must be best-effort when the caller already
    // has a token or an explicit --client-id, so login still completes.

    [Fact]
    public async Task LoginAsync_Should_Succeed_WithAccessToken_When_SettingsEndpointBlockedByTenant()
    {
        // Arrange — the deployed API blocks the anonymous discovery endpoint.
        var apiClient = new FakeAonikCliApiClient
        {
            AuthSettingsException = new AonikCliException(
                "AONIK API call failed with status 401 (Unauthorized): {\"error\":\"Tenant context missing\"}")
        };
        var sessionStore = new InMemorySessionStore();
        var handler = new AuthCommandHandler(apiClient, sessionStore, new TextWriterCliOutputWriter(new StringWriter()));

        // Act
        var exitCode = await handler.LoginAsync(
            new LoginOptions(
                BaseUrl: "https://aonik-dev-api.example.azurecontainerapps.io",
                Username: null,
                Password: null,
                AccessToken: "header.payload.signature",
                ClientId: null,
                Scope: null,
                TenantId: null,
                OutputMode: OutputMode.Json));

        // Assert — login completes; token stored and userinfo resolved despite the block.
        exitCode.Should().Be(0);
        sessionStore.Session.Should().NotBeNull();
        sessionStore.Session!.AccessToken.Should().Be("header.payload.signature");
        sessionStore.Session.Email.Should().Be("operator@aonik.io");
    }

    [Fact]
    public async Task LoginAsync_Should_Succeed_WithPasswordAndExplicitClientId_When_SettingsEndpointBlocked()
    {
        // Arrange — explicit --client-id removes the need for provider discovery.
        var apiClient = new FakeAonikCliApiClient
        {
            AuthSettingsException = new AonikCliException("401 Tenant context missing")
        };
        var sessionStore = new InMemorySessionStore();
        var handler = new AuthCommandHandler(apiClient, sessionStore, new TextWriterCliOutputWriter(new StringWriter()));

        // Act
        var exitCode = await handler.LoginAsync(
            new LoginOptions(
                BaseUrl: "https://aonik-dev-api.example.azurecontainerapps.io",
                Username: "user@example.com",
                Password: "secret",
                AccessToken: null,
                ClientId: "explicit-client-id",
                Scope: "openid",
                TenantId: null,
                OutputMode: OutputMode.Json));

        // Assert — the password grant runs against the explicit client-id; session saved.
        exitCode.Should().Be(0);
        sessionStore.Session.Should().NotBeNull();
        sessionStore.Session!.AccessToken.Should().Be("token");
    }

    [Fact]
    public async Task LoginAsync_Should_Propagate_WithPasswordAndNoClientId_When_SettingsEndpointBlocked()
    {
        // Arrange — a password grant with no token and no client-id genuinely needs
        // discovery, so a blocked settings endpoint must surface (we cannot proceed).
        var apiClient = new FakeAonikCliApiClient
        {
            AuthSettingsException = new AonikCliException("401 Tenant context missing")
        };
        var sessionStore = new InMemorySessionStore();
        var handler = new AuthCommandHandler(apiClient, sessionStore, new TextWriterCliOutputWriter(new StringWriter()));

        // Act
        var act = async () => await handler.LoginAsync(
            new LoginOptions(
                BaseUrl: "https://aonik-dev-api.example.azurecontainerapps.io",
                Username: "user@example.com",
                Password: "secret",
                AccessToken: null,
                ClientId: null,
                Scope: null,
                TenantId: null,
                OutputMode: OutputMode.Json));

        // Assert
        await act.Should().ThrowAsync<AonikCliException>().WithMessage("*Tenant context missing*");
    }
}
