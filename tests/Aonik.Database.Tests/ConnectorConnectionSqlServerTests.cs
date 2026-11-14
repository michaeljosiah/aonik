using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class ConnectorConnectionSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task Requests_Should_DeduplicateConcurrentSubmissions_AndCommitOnlyTheWinningOutbox()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(); Guid userId = Guid.NewGuid();
        await SeedAsync(tenantId, userId);
        var input = new CreateConnectionRequest("gmail", "User", [new(GmailActionKeys.Search, 1)], "Invoice correspondence", "same-key");
        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using PlatformDbContext db = Context(tenantId);
            return (await Services(db, tenantId, userId).Requests.CreateAsync(input)).Id;
        }));

        ids.Distinct().Should().ContainSingle();
        await using PlatformDbContext verify = Context(tenantId);
        (await verify.ConnectorConnectionRequests.CountAsync()).Should().Be(1);
        (await verify.Set<OutboxMessage>().CountAsync(message => message.TenantId == tenantId)).Should().Be(2);
    }

    [SkippableFact]
    public async Task GrantIssuance_Should_RollBack_WhenTheSelectedAccountWasConcurrentlyDisconnected()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(); Guid userId = Guid.NewGuid();
        await SeedAsync(tenantId, userId);
        Guid connectionId;
        await using (PlatformDbContext seed = Context(tenantId))
        {
            var account = new ConnectorConnection { TenantId = tenantId, ConnectorKey = "gmail", Ownership = "User", OwnerUserId = userId,
                ProvisionedByUserId = userId, ProviderAccountId = "account-1", Status = "Active", GrantedScopesJson = ConnectorJson.Serialize(new[] { GmailActionKeys.ReadScope }) };
            seed.ConnectorConnections.Add(account); await seed.SaveChangesAsync(); connectionId = account.Id;
        }
        await using PlatformDbContext stale = Context(tenantId);
        ConnectorConnection selected = await stale.ConnectorConnections.SingleAsync(connection => connection.Id == connectionId);
        string token = Convert.ToBase64String(selected.RowVersion);
        await using (PlatformDbContext concurrent = Context(tenantId))
        {
            ConnectorConnection account = await concurrent.ConnectorConnections.SingleAsync(connection => connection.Id == connectionId);
            account.Status = "Revoked"; await concurrent.SaveChangesAsync();
        }
        Func<Task> issue = () => Services(stale, tenantId, userId).Grants.IssueAsync(connectionId, new("User", userId, [new(GmailActionKeys.Search, 1)], token));

        await issue.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "Conflict");
        await using PlatformDbContext verify = Context(tenantId);
        (await verify.ConnectorConnectionGrants.CountAsync()).Should().Be(0);
        (await verify.ConnectorConnections.SingleAsync(connection => connection.Id == connectionId)).Status.Should().Be("Revoked");
    }

    private async Task SeedAsync(Guid tenantId, Guid userId)
    {
        await using PlatformDbContext db = Context(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = $"Connector SQL tests {tenantId:N}", Status = "Active" });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Status = "Active", ExternalSubject = userId.ToString() });
        db.Settings.AddRange(new Setting { Key = IntegrationSettingNames.GmailEnabled, Scope = SettingScope.Tenant, TenantId = tenantId, Value = "true" },
            new Setting { Key = IntegrationSettingNames.GmailAllowSelfService, Scope = SettingScope.Tenant, TenantId = tenantId, Value = "true" });
        await db.SaveChangesAsync();
    }
    private PlatformDbContext Context(Guid tenantId) => new(database.CreateOptions<PlatformDbContext>(), new TestTenantProvider(tenantId));
    private static (ConnectorConnectionRequestService Requests, ConnectorGrantService Grants) Services(PlatformDbContext db, Guid tenantId, Guid userId)
    {
        var tenant = new TestTenantProvider(tenantId); var permissions = new AllowAllPermissionService(); var clock = new Clock();
        var catalog = new ConnectorCatalog([new GmailConnectorDescriptor()]);
        var policies = new ConnectorPolicyService(db, tenant, new ConfigurationBuilder().Build());
        var roles = new TenantRoleReader(db, tenant);
        var auth = new ConnectorAuthorization(db, tenant, new TestCurrentUserProvider(userId), permissions, new UserDirectoryReader(db), roles, policies, clock);
        var audit = new Mock<IAuditLogWriter>();
        audit.Setup(writer => writer.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var grants = new ConnectorGrantService(db, auth, catalog, policies, roles, audit.Object, clock);
        var oauth = new Mock<IConnectorOAuthProvider>(); oauth.SetupGet(provider => provider.ConnectorKey).Returns("gmail"); oauth.SetupGet(provider => provider.IsConfigured).Returns(true);
        var action = new Mock<IConnectorActionProvider>(); action.SetupGet(provider => provider.ConnectorKey).Returns("gmail");
        return (new(db, auth, catalog, policies, grants, [oauth.Object], [action.Object], roles, permissions, audit.Object, clock), grants);
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
