using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Platform.Integrations;

public class ConnectorAuthorizationTests
{
    [Fact]
    public async Task TenantPolicy_Should_IgnoreUserOverrides_AndRequireExplicitTenantEnablement()
    {
        using var host = new Host();
        host.Setting(IntegrationSettingNames.GmailEnabled, "true", SettingScope.Global);
        host.Setting(IntegrationSettingNames.GmailEnabled, "true", SettingScope.User);
        await host.Database.SaveChangesAsync();
        (await host.Policy.GetAsync("gmail")).Code.Should().Be("TenantNotEnabled");

        host.Setting(IntegrationSettingNames.GmailEnabled, "false", SettingScope.Tenant);
        await host.Database.SaveChangesAsync();
        (await host.Policy.GetAsync("gmail")).Code.Should().Be("ForbiddenByPolicy");
    }

    [Theory]
    [InlineData(IntegrationSettingNames.GmailAllowUserConnections)]
    [InlineData(IntegrationSettingNames.GmailAllowedActions)]
    public async Task TenantRestrictions_Should_DenyAnOtherwiseGrantedPersonalConnection(string restriction)
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        host.Setting(restriction, restriction == IntegrationSettingNames.GmailAllowedActions ? "[]" : "false", SettingScope.Tenant);
        host.Setting(restriction, restriction == IntegrationSettingNames.GmailAllowedActions ? ConnectorJson.Serialize(host.AllActions) : "true", SettingScope.User);
        await host.Database.SaveChangesAsync();

        Func<Task> invoke = async () => { await host.Use(connection.Id); };

        (await invoke.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("ForbiddenByPolicy");
    }

    [Fact]
    public async Task OrganizationalOwnershipAndSettingsAdministration_Should_NotGrantMailboxUse()
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection(grant: false, ownership: "Tenant");
        (await host.Authorization.CanManageAsync(host.UserId)).Should().BeTrue();

        Func<Task> invoke = async () => { await host.Use(connection.Id); };

        (await invoke.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("NoAuthorizedConnection");
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("scope")]
    [InlineData("owner")]
    [InlineData("member")]
    public async Task CurrentGrantsScopesAndMembership_Should_BeCheckedOnEveryUse(string change)
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        await host.Use(connection.Id);
        ConnectorConnectionGrant grant = await host.Database.ConnectorConnectionGrants.SingleAsync();
        if (change == "revoked") grant.RevokedAt = host.Clock.UtcNow;
        if (change == "expired") grant.ExpiresAt = host.Clock.UtcNow;
        if (change == "scope") connection.GrantedScopesJson = "[]";
        if (change == "owner") host.Inactive.Add(host.OwnerId);
        if (change == "member") host.Inactive.Add(host.UserId);
        await host.Database.SaveChangesAsync();

        Func<Task> invoke = async () => { await host.Use(connection.Id); };

        if (change == "member") await invoke.Should().ThrowAsync<PermissionDeniedException>();
        else await invoke.Should().ThrowAsync<ConnectorAccessException>();
    }

    [Fact]
    public async Task RoleGrant_Should_StopWorking_WhenCurrentRoleMembershipIsRemoved()
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        ConnectorConnectionGrant grant = await host.Database.ConnectorConnectionGrants.SingleAsync();
        grant.GranteeType = "Role"; grant.GranteeId = host.RoleId;
        await host.Database.SaveChangesAsync();
        await host.Use(connection.Id);
        host.InRole = false;

        Func<Task> invoke = async () => { await host.Use(connection.Id); };

        (await invoke.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("NoAuthorizedConnection");
    }

    [Fact]
    public async Task WorkflowGrant_Should_RequireTheActorGrantAndPinnedActionAllowlist_DespiteLaterGrantExpansion()
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        Guid workflowId = Guid.NewGuid();
        var workflowGrant = new ConnectorConnectionGrant { TenantId = host.TenantId, ConnectionId = connection.Id, GranteeType = "Workflow", GranteeId = workflowId,
            AllowedActionsJson = ConnectorJson.Serialize(host.AllActions), IssuedByUserId = host.OwnerId };
        host.Database.ConnectorConnectionGrants.Add(workflowGrant);
        host.Setting(IntegrationSettingNames.GmailAllowWorkflowConnections, "true", SettingScope.Tenant);
        await host.Database.SaveChangesAsync();
        var context = new ConnectorUseContext(new("User", host.UserId, host.UserId), workflowId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), workflowGrant.Id,
            [new(GmailActionKeys.Search, 1)]);
        await host.Authorization.RequireUseAsync(connection.Id, host.Catalog.Resolve(new(GmailActionKeys.Search, 1)), context);

        Func<Task> expanded = async () => { await host.Authorization.RequireUseAsync(connection.Id,
            host.Catalog.Resolve(new(GmailActionKeys.GetMessage, 1)), context); };
        (await expanded.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("WorkflowBindingRequired");
        (await host.Database.ConnectorConnectionGrants.SingleAsync(grant => grant.GranteeType == "User")).RevokedAt = host.Clock.UtcNow;
        await host.Database.SaveChangesAsync();
        Func<Task> noActorGrant = async () => { await host.Authorization.RequireUseAsync(connection.Id,
            host.Catalog.Resolve(new(GmailActionKeys.Search, 1)), context); };
        (await noActorGrant.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("NoAuthorizedConnection");
    }

    [Fact]
    public async Task AnotherTenant_Should_NeverSeeOrUseTheConnection()
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        using var other = new Host(host.DatabaseName);

        Func<Task> invoke = async () => { await other.Use(connection.Id); };

        (await invoke.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("NoAuthorizedConnection");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("connection")]
    [InlineData("account")]
    [InlineData("version")]
    public async Task ProtectedCredentials_Should_RejectSwappingAcrossTheirBoundIdentity(string change)
    {
        using var host = new Host();
        ConnectorConnection connection = await host.Connection();
        var protector = new ConnectorCredentialProtector(new EphemeralDataProtectionProvider());
        var tokens = new ConnectorOAuthTokens("synthetic-access", "synthetic-refresh", host.Clock.UtcNow.AddHours(1), [GmailActionKeys.ReadScope]);
        connection.ProtectedCredentials = protector.Protect(connection, tokens);
        connection.ProtectedCredentials.Should().NotContain("synthetic-access").And.NotContain("synthetic-refresh");
        protector.Read(connection).AccessToken.Should().Be(tokens.AccessToken);
        if (change == "tenant") connection.TenantId = Guid.NewGuid();
        if (change == "connection") connection.Id = Guid.NewGuid();
        if (change == "account") connection.ProviderAccountId = "another-account";
        if (change == "version") connection.CredentialVersion++;

        Action read = () => protector.Read(connection);

        read.Should().Throw<ConnectorAccessException>().Which.Code.Should().Be("ReauthorizationRequired");
    }

    private sealed class Host : IDisposable
    {
        public string DatabaseName { get; }
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid OwnerId { get; } = Guid.NewGuid();
        public Guid RoleId { get; } = Guid.NewGuid();
        public HashSet<Guid> Inactive { get; } = [];
        public bool InRole { get; set; } = true;
        public TestClock Clock { get; } = new();
        public PlatformDbContext Database { get; }
        public ConnectorPolicyService Policy { get; }
        public ConnectorAuthorization Authorization { get; }
        public ConnectorCatalog Catalog { get; } = new([new GmailConnectorDescriptor()]);
        public IReadOnlyList<ConnectorActionRef> AllActions => GmailActionKeys.ReadActions.Select(key => new ConnectorActionRef(key, 1)).ToList();

        public Host(string? databaseName = null)
        {
            DatabaseName = databaseName ?? $"ConnectorAuthorization_{Guid.NewGuid()}";
            var tenant = new TestTenantProvider(TenantId);
            Database = new(new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase(DatabaseName).Options, tenant);
            var users = new Mock<IUserDirectoryReader>();
            users.Setup(reader => reader.GetByIdsAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<UserDirectoryItem>)ids.Select(id => new UserDirectoryItem(id, null, Inactive.Contains(id) ? "Inactive" : "Active")).ToList());
            var roles = new Mock<ITenantRoleReader>();
            roles.Setup(reader => reader.IsMemberAsync(TenantId, UserId, RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(() => InRole);
            Policy = new(Database, tenant, new ConfigurationBuilder().Build());
            Authorization = new(Database, tenant, new TestCurrentUserProvider(UserId), new AllowAllPermissionService(), users.Object, roles.Object, Policy, Clock);
        }
        public void Setting(string key, string value, SettingScope scope) => Database.Settings.Add(new Setting
        { Key = key, Value = value, Scope = scope, TenantId = scope == SettingScope.Global ? null : TenantId, UserId = scope == SettingScope.User ? UserId : null });
        public async Task<ConnectorConnection> Connection(bool grant = true, string ownership = "User")
        {
            Setting(IntegrationSettingNames.GmailEnabled, "true", SettingScope.Tenant);
            var connection = new ConnectorConnection { TenantId = TenantId, ConnectorKey = "gmail", Ownership = ownership, OwnerUserId = ownership == "User" ? OwnerId : null,
                ProvisionedByUserId = OwnerId, DisplayName = "Test mailbox", ProviderAccountId = "provider-account-1", CredentialVersion = 1, Status = "Active",
                GrantedScopesJson = ConnectorJson.Serialize(new[] { GmailActionKeys.ReadScope }) };
            Database.ConnectorConnections.Add(connection);
            if (grant) Database.ConnectorConnectionGrants.Add(new() { TenantId = TenantId, ConnectionId = connection.Id, GranteeType = "User", GranteeId = UserId,
                AllowedActionsJson = ConnectorJson.Serialize(AllActions), IssuedByUserId = OwnerId });
            await Database.SaveChangesAsync();
            return connection;
        }
        public Task<ConnectorConnection> Use(Guid connectionId) => Authorization.RequireUseAsync(connectionId, Catalog.Resolve(new(GmailActionKeys.Search, 1)), new(new("User", UserId, UserId)));
        public void Dispose() => Database.Dispose();
    }
    private sealed class TestClock : IClock { public DateTime UtcNow => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
}
