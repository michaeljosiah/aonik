using System.Security.Claims;

using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Caching;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Infrastructure.Tests.Settings;

public class IntegrationSettingsTests
{
    [Theory]
    [InlineData("Integrations.Gmail.Enabled")]
    [InlineData("integrations.gmail.allowselfservice")]
    [InlineData("Integrations.Gmail.AllowedActions")]
    [InlineData("Workflows.ExecutionEnabled")]
    public async Task UserSettings_Should_NotOverrideIntegrationOrWorkflowPolicy(string key)
    {
        using var host = new Host();
        Func<Task> change = () => host.Service.SetAsync(key, "true", SettingScope.User, host.TenantId, host.UserId);
        await change.Should().ThrowAsync<PermissionDeniedException>();
        (await host.Db.Settings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GlobalSettings_Should_RequireActualPlatformAuthorization_EvenForTenantManagers()
    {
        using var host = new Host();
        Func<Task> change = () => host.Service.SetAsync(IntegrationSettingNames.GmailEnabled, "true", SettingScope.Global);
        await change.Should().ThrowAsync<PermissionDeniedException>().Where(error => error.PermissionKey == "Integration.ManagePlatform");
        host.PlatformAdmin = true;
        await change();
        (await host.Db.Settings.SingleAsync()).Scope.Should().Be(SettingScope.Global);
    }

    [Fact]
    public async Task DefaultSelection_Should_RevalidateAccountEligibility_AndNeverAcceptAnotherTenantOrUserScope()
    {
        using var host = new Host();
        Guid connectionId = Guid.NewGuid();
        host.Preference.Setup(service => service.RequireEligibleAsync(connectionId, host.UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConnectorAccessException("NoAuthorizedConnection", "Not granted."));
        Func<Task> denied = () => host.Service.SetAsync(IntegrationSettingNames.GmailDefaultConnectionId, connectionId.ToString(), SettingScope.User, host.TenantId, host.UserId);
        await denied.Should().ThrowAsync<ConnectorAccessException>();
        host.Preference.Setup(service => service.RequireEligibleAsync(connectionId, host.UserId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await denied();
        (await host.Db.Settings.SingleAsync()).Value.Should().Be(connectionId.ToString());
        Func<Task> other = () => host.Service.SetAsync(IntegrationSettingNames.GmailDefaultConnectionId, connectionId.ToString(), SettingScope.User, host.TenantId, Guid.NewGuid());
        await other.Should().ThrowAsync<PermissionDeniedException>();
        Func<Task> otherTenant = () => host.Service.SetAsync(IntegrationSettingNames.GmailEnabled, "true", SettingScope.Tenant, Guid.NewGuid());
        await otherTenant.Should().ThrowAsync<PermissionDeniedException>();
    }

    private sealed class Host : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public bool PlatformAdmin { get; set; }
        public AonikDbContext Db { get; }
        public Mock<IConnectorPreferenceValidator> Preference { get; } = new();
        public SettingService Service { get; }
        public Host()
        {
            var tenant = new TestTenantProvider(TenantId);
            var currentUser = new TestCurrentUserProvider(UserId);
            Db = new(new DbContextOptionsBuilder<AonikDbContext>().UseInMemoryDatabase($"IntegrationSettings_{Guid.NewGuid()}").Options, tenant, currentUser);
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(service => service.HasPermissionAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "PlatformAdmin"))
                .ReturnsAsync(() => PlatformAdmin ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            Service = new(Db, new ConfigurationBuilder().Build(), new PassthroughCache(), Mock.Of<ISettingValueProtector>(), tenant,
                currentUser, permissions.Object, new CacheInvalidationPublisher(), authorization.Object,
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString())], "Test")) } }, Preference.Object);
        }
        public void Dispose() => Db.Dispose();
    }
    private sealed class PassthroughCache : ICacheStore
    {
        public Task<T?> GetOrSetAsync<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T?>> factory, string cacheSet, CancellationToken cancellationToken = default)
            => factory(cancellationToken);
    }
}
