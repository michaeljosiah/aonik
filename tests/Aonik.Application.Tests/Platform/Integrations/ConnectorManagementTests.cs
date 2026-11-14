using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Application.Tests.Platform.Integrations;

public class ConnectorManagementTests
{
    [Fact]
    public void RequestLimiter_Should_BoundNewRequests_IndependentlyForEachTenantAndUser()
    {
        using var limiter = new ConnectorRequestRateLimiter();
        Guid tenant = Guid.NewGuid();
        Guid user = Guid.NewGuid();
        for (int index = 0; index < 10; index++) limiter.Require(tenant, user);

        Action exceeded = () => limiter.Require(tenant, user);

        exceeded.Should().Throw<ConnectorAccessException>().Where(error => error.Code == "RateLimited");
        limiter.Require(tenant, Guid.NewGuid());
        limiter.Require(Guid.NewGuid(), user);
    }

    [Fact]
    public async Task Disconnect_Should_RevokeGrantsAndPrivateCredentials_BeforeProviderRevocation()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);
        var granted = await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = await db.ConnectorConnections.SingleAsync();
        scenario.OAuth.Setup(provider => provider.RevokeAsync("synthetic-refresh", It.IsAny<CancellationToken>())).Returns(async () =>
        {
            using var observer = scenario.Scope();
            var persisted = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().ConnectorConnections.SingleAsync();
            persisted.Status.Should().Be("Revoked");
            persisted.ProtectedCredentials.Should().BeEmpty();
        });

        await scope.ServiceProvider.GetRequiredService<ConnectorConnectionManagementService>().DisconnectAsync(granted.ConnectionId!.Value, Convert.ToBase64String(connection.RowVersion));

        (await db.ConnectorConnectionGrants.SingleAsync()).RevokedAt.Should().Be(scenario.Clock.UtcNow);
        scenario.OAuth.Verify(provider => provider.RevokeAsync("synthetic-refresh", It.IsAny<CancellationToken>()), Times.Once);
        Func<Task> read = () => scope.ServiceProvider.GetRequiredService<ConnectorService>().InvokeReadAsync(connection.Id, new(GmailActionKeys.Search, 1), ConnectorJson.Element(new GmailSearchRequest("invoice")));
        await read.Should().ThrowAsync<ConnectorAccessException>();
        scenario.Actions.Verify(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reconnect_Should_RefuseReplacingAnExistingConnectionWithAnotherProviderAccount()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);
        var granted = await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = await db.ConnectorConnections.SingleAsync();
        string priorCredentials = connection.ProtectedCredentials;
        var reconnect = await requests.CreateAsync(scenario.Request("reconnect") with { ConnectionId = granted.ConnectionId });
        var retry = await consent.BeginAsync(reconnect.Id, new(reconnect.ExpectedVersion));
        scenario.Account = new("different-account", "other@example.test");

        Func<Task> replace = () => consent.ReceiveCallbackAsync(retry.Cookie, ConnectorScenario.State(retry), "other-code", scenario.Current.OwnerId);

        await replace.Should().ThrowAsync<ConnectorProviderException>().Where(error => error.Code == "ProviderAccountChanged");
        await db.Entry(connection).ReloadAsync();
        connection.ProviderAccountId.Should().Be("google-account-1");
        connection.ProtectedCredentials.Should().Be(priorCredentials);
    }
}
