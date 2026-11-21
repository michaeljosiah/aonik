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
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Platform.Integrations;

public class ConnectorSubscriptionTests
{
    [Fact]
    public async Task InitialSync_Should_SuppressOldMail_AndCommitCursorWithNewSourceReferences()
    {
        using var host = await Host.CreateAsync();
        host.Provider.Messages["old"] = host.Message("old", host.Clock.UtcNow.AddDays(-1));
        host.Provider.Messages["new"] = host.Message("new", host.Clock.UtcNow);
        host.Provider.List = (_, _) => new([host.Provider.Messages["old"], host.Provider.Messages["new"]], null);
        Guid id = await host.ActivateAsync();

        await host.Service.SynchronizeAsync(id);

        var subscription = await host.Database.ConnectorSubscriptions.SingleAsync();
        subscription.HistoryId.Should().Be("100"); subscription.FullSync.Should().BeFalse(); subscription.LeaseId.Should().BeNull();
        var discovered = await host.DiscoveredAsync();
        discovered.Should().ContainSingle().Which.MessageId.Should().Be("new");
        host.Provider.RequestedSince.Should().Be(host.Clock.UtcNow);
        (await host.Database.Set<OutboxMessage>().SingleAsync()).Payload.Should().NotContain("synthetic-token").And.NotContain("mailbox@example.test");
    }

    [Fact]
    public async Task InterruptedPage_Should_RetainProgress_AndResumeWithoutRepeatingCommittedMail()
    {
        using var host = await Host.CreateAsync();
        host.Provider.Messages["one"] = host.Message("one", host.Clock.UtcNow); host.Provider.Messages["two"] = host.Message("two", host.Clock.UtcNow);
        bool failSecond = true;
        host.Provider.List = (page, _) => page is null ? new([host.Provider.Messages["one"]], "next")
            : failSecond ? throw new ConnectorProviderException("ProviderUnavailable", true) : new([host.Provider.Messages["two"]], null);
        Guid id = await host.ActivateAsync();
        Func<Task> interrupted = () => host.Service.SynchronizeAsync(id);
        await interrupted.Should().ThrowAsync<ConnectorProviderException>();
        var pending = await host.Database.ConnectorSubscriptions.SingleAsync();
        pending.PageToken.Should().Be("next"); pending.FullSync.Should().BeTrue(); pending.HistoryId.Should().BeNull();
        (await host.DiscoveredAsync()).Select(value => value.MessageId).Should().Equal("one");
        failSecond = false;

        await host.Service.SynchronizeAsync(id);

        var finished = await host.Database.ConnectorSubscriptions.SingleAsync();
        finished.PageToken.Should().BeNull(); finished.HistoryId.Should().Be("100");
        (await host.DiscoveredAsync()).Select(value => value.MessageId).Should().BeEquivalentTo("one", "two");
    }

    [Fact]
    public async Task ExpiredCursor_Should_UseABoundedFullSync_WithoutHistoricalAutomation()
    {
        using var host = await Host.CreateAsync(); Guid id = await host.ActivateAsync(); await host.Service.SynchronizeAsync(id);
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(20);
        host.Provider.ProfileHistory = "250";
        host.Provider.History = (_, _) => throw new ConnectorProviderException("MailboxCursorExpired");
        host.Provider.Messages["historical"] = host.Message("historical", host.Clock.UtcNow.AddDays(-2));
        host.Provider.Messages["current"] = host.Message("current", host.Clock.UtcNow);
        host.Provider.List = (_, _) => new(host.Provider.Messages.Values.ToList(), null);

        await host.Service.SynchronizeAsync(id);

        (await host.Database.ConnectorSubscriptions.SingleAsync()).HistoryId.Should().Be("250");
        (await host.DiscoveredAsync()).Select(value => value.MessageId).Should().Equal("current");
        host.Provider.RequestedSince.Should().Be(host.Clock.UtcNow);
    }

    [Fact]
    public async Task RevocationDuringProviderRead_Should_DiscardThePageAndLeaveTheCursorUnadvanced()
    {
        using var host = await Host.CreateAsync(); Guid id = await host.ActivateAsync(); await host.Service.SynchronizeAsync(id);
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(20);
        host.Provider.Messages["new"] = host.Message("new", host.Clock.UtcNow);
        host.Provider.History = (_, _) => new("120", [new("110", "new", "thread")], null);
        host.Provider.BeforeGet = async () => { (await host.Database.ConnectorConnectionGrants.SingleAsync(value => value.GranteeType == "AutomationIdentity")).RevokedAt = host.Clock.UtcNow; await host.Database.SaveChangesAsync(); };

        Func<Task> synchronize = () => host.Service.SynchronizeAsync(id);

        await synchronize.Should().ThrowAsync<ConnectorProviderException>().Where(value => value.Code == "NoAuthorizedConnection");
        var held = await host.Database.ConnectorSubscriptions.SingleAsync();
        held.HistoryId.Should().Be("100"); held.Status.Should().Be("Suspended");
        (await host.DiscoveredAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderWatchConflict_Should_RequireApprovedPolling_AndNeverReplaceAnotherTenantsWatch(bool fallback)
    {
        string databaseName = $"MailboxWatch_{Guid.NewGuid()}";
        using var first = await Host.CreateAsync(databaseName, push: true);
        Guid firstId = await first.ActivateAsync(); await first.Service.SynchronizeAsync(firstId);
        using var second = await Host.CreateAsync(databaseName, push: true, fallback: fallback);

        Func<Task<Guid>> activate = () => second.ActivateAsync();

        if (fallback)
        {
            Guid id = await activate();
            var subscription = await second.Database.ConnectorSubscriptions.SingleAsync(value => value.Id == id);
            subscription.Mode.Should().Be("Polling"); subscription.WatchClaim.Should().BeNull(); subscription.LastError.Should().Be("MailboxWatchConflict");
        }
        else await activate.Should().ThrowAsync<ConnectorAccessException>().Where(value => value.Code == "MailboxWatchConflict");
        first.Provider.WatchCalls.Should().Be(1); second.Provider.WatchCalls.Should().Be(0);
    }

    [Fact]
    public async Task SharedConnection_Should_ServeTwoBindings_AndRenewItsSingleWatchDaily()
    {
        using var host = await Host.CreateAsync(push: true); Guid id = await host.ActivateAsync();
        var second = host.Binding with { BindingId = Guid.NewGuid() }; host.ActiveBindings.Add(second);
        (await host.Service.ActivateAsync(second.BindingId, second.Revision)).Should().Be(id);
        await host.Service.SynchronizeAsync(id); host.Provider.WatchCalls.Should().Be(1);
        host.Clock.UtcNow = host.Clock.UtcNow.AddDays(1);

        await host.Service.SynchronizeAsync(id);

        host.Provider.WatchCalls.Should().Be(2); (await host.Database.ConnectorSubscriptions.CountAsync()).Should().Be(1);
        host.ActiveBindings.Clear();
        await host.Service.DisableBindingAsync(host.Binding.BindingId, host.Binding.Revision);
        (await host.Database.ConnectorSubscriptions.SingleAsync()).Status.Should().Be("Active", "the second binding still retains its membership until its own disable is processed");
        await host.Service.DisableBindingAsync(second.BindingId, second.Revision);
        var disabled = await host.Database.ConnectorSubscriptions.SingleAsync();
        disabled.Status.Should().Be("Disabled"); disabled.WatchClaim.Should().BeNull(); host.Provider.StopCalls.Should().Be(1);
    }

    [Fact]
    public async Task PollingTask_Should_RejectAProofCopiedToAnotherTenantOrTask()
    {
        using var host = await Host.CreateAsync(); await host.ActivateAsync(); var recorded = host.Scheduled.Single();
        Action otherTenant = () => host.Service.ReadTask(recorded.ActionPayloadJson, Guid.NewGuid(), recorded.TaskId!.Value);
        Action otherTask = () => host.Service.ReadTask(recorded.ActionPayloadJson, host.TenantId, Guid.NewGuid());
        otherTenant.Should().Throw<ArgumentException>(); otherTask.Should().Throw<ArgumentException>();
    }

    private sealed class Host : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid ActorId { get; } = Guid.NewGuid(); public Guid WorkflowId { get; } = Guid.NewGuid(); public Guid VersionId { get; } = Guid.NewGuid();
        public Clock Clock { get; } = new(); public Provider Provider { get; } = new(); public PlatformDbContext Database { get; }
        public ConnectorSubscriptionService Service { get; } public ConnectorSubscriptionBinding Binding { get; private set; } = null!;
        public List<ConnectorSubscriptionBinding> ActiveBindings { get; } = []; public List<ScheduleTaskRequest> Scheduled { get; } = [];
        private readonly TestTenantProvider _tenant; private readonly ConnectorCredentialProtector _protector;
        private Host(string? databaseName)
        {
            _tenant = new(TenantId); Database = new(new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase(databaseName ?? $"MailboxSync_{Guid.NewGuid()}").Options, _tenant);
            Provider.Now = () => Clock.UtcNow;
            var dataProtection = new EphemeralDataProtectionProvider(); _protector = new(dataProtection);
            var permissions = new AllowAllPermissionService(); var users = new Mock<IUserDirectoryReader>(); var roles = new Mock<ITenantRoleReader>();
            var automation = new Mock<IAutomationIdentityAuthorization>();
            automation.Setup(value => value.RequireAsync(It.IsAny<WorkflowActor>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<AutomationActionRef?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
            var policy = new ConnectorPolicyService(Database, _tenant, new ConfigurationBuilder().Build());
            var authorization = new ConnectorAuthorization(Database, _tenant, new TestCurrentUserProvider(Guid.NewGuid()), permissions, users.Object, roles.Object, policy, Clock, automation.Object);
            var reader = new Mock<IWorkflowMailboxBindingReader>();
            reader.Setup(value => value.RequireActiveAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, int revision, CancellationToken _) => Task.FromResult(ActiveBindings.SingleOrDefault(value => value.BindingId == id && value.Revision == revision) ?? throw new PermissionDeniedException("Workflow.Run")));
            reader.Setup(value => value.IsDisabledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => ActiveBindings.All(value => value.BindingId != id));
            var tasks = new Mock<ITaskService>(); tasks.Setup(value => value.ScheduleAsync(It.IsAny<ScheduleTaskRequest>(), It.IsAny<CancellationToken>()))
                .Callback((ScheduleTaskRequest request, CancellationToken _) => Scheduled.Add(request)).ReturnsAsync((TaskResponse)null!);
            var credentials = new ConnectorCredentialService(Database, _tenant, _protector, [], Clock);
            Service = new(Database, _tenant, reader.Object, authorization, new ConnectorCatalog([new GmailConnectorDescriptor()]), credentials, [Provider], dataProtection, tasks.Object, Clock);
        }
        public static async Task<Host> CreateAsync(string? databaseName = null, bool push = false, bool fallback = false)
        {
            var host = new Host(databaseName);
            foreach (string key in new[] { IntegrationSettingNames.GmailEnabled, IntegrationSettingNames.GmailAllowWorkflowConnections })
                host.Database.Settings.Add(new Setting { TenantId = host.TenantId, Key = key, Value = "true", Scope = SettingScope.Tenant });
            var connection = new ConnectorConnection { TenantId = host.TenantId, Ownership = "Tenant", ConnectorKey = "gmail", ProviderAccountId = "account", ProviderAccountLabel = "mailbox@example.test", Status = "Active", CredentialVersion = 1,
                GrantedScopesJson = ConnectorJson.Serialize(new[] { GmailActionKeys.ReadScope }) };
            connection.ProtectedCredentials = host._protector.Protect(connection, new("synthetic-token", null, host.Clock.UtcNow.AddDays(2), [GmailActionKeys.ReadScope]));
            var workflowGrant = new ConnectorConnectionGrant { TenantId = host.TenantId, ConnectionId = connection.Id, GranteeType = "Workflow", GranteeId = host.WorkflowId, AllowedActionsJson = ConnectorJson.Serialize(new[] { new ConnectorActionRef(GmailActionKeys.Search, 1) }) };
            host.Database.ConnectorConnections.Add(connection); host.Database.ConnectorConnectionGrants.Add(workflowGrant);
            host.Database.ConnectorConnectionGrants.Add(new() { TenantId = host.TenantId, ConnectionId = connection.Id, GranteeType = "AutomationIdentity", GranteeId = host.ActorId, AllowedActionsJson = workflowGrant.AllowedActionsJson });
            host.Binding = new(Guid.NewGuid(), 1, host.WorkflowId, host.VersionId, host.ActorId, new(connection.Id, workflowGrant.Id, [new(GmailActionKeys.Search, 1)]), push ? "PushWithPolling" : "Polling", 15, ["INBOX"], fallback, null, host.Clock.UtcNow);
            host.ActiveBindings.Add(host.Binding); await host.Database.SaveChangesAsync(); return host;
        }
        public Task<Guid> ActivateAsync() => Service.ActivateAsync(Binding.BindingId, Binding.Revision);
        public MailboxMessageReference Message(string id, DateTime received) => new(id, "thread", received, ["INBOX"]);
        public async Task<List<ConnectorMessageAddedEvent>> DiscoveredAsync() => (await Database.Set<OutboxMessage>().AsNoTracking().Where(value => value.EventType == typeof(ConnectorMessageAddedEvent).FullName).ToListAsync())
            .Select(value => System.Text.Json.JsonSerializer.Deserialize<ConnectorMessageAddedEvent>(value.Payload, OutboxSerialization.Options)!).ToList();
        public void Dispose() => Database.Dispose();
    }
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class Provider : IConnectorMailboxSyncProvider
    {
        public string ConnectorKey => "gmail"; public bool IsConfigured => true; public string ApplicationKey => "reviewed-app";
        public string? PushTopic => "projects/example/topics/gmail"; public string? PushSubscription => "projects/example/subscriptions/gmail";
        public string ProfileHistory { get; set; } = "100"; public int WatchCalls { get; private set; } public int StopCalls { get; private set; }
        public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;
        public Dictionary<string, MailboxMessageReference> Messages { get; } = []; public Func<Task>? BeforeGet { get; set; } public DateTime? RequestedSince { get; private set; }
        public Func<string?, CancellationToken, MailboxMessagePage> List { get; set; } = (_, _) => new([], null);
        public Func<string?, CancellationToken, MailboxHistoryPage> History { get; set; } = (_, _) => new("200", [], null);
        public Task<MailboxProfile> GetProfileAsync(MailboxProviderCall call, CancellationToken cancellationToken = default) => Task.FromResult(new MailboxProfile("mailbox@example.test", ProfileHistory));
        public Task<MailboxWatch> WatchAsync(MailboxProviderCall call, string topic, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
        { WatchCalls++; return Task.FromResult(new MailboxWatch(ProfileHistory, Now().AddDays(6))); }
        public Task StopWatchAsync(MailboxProviderCall call, CancellationToken cancellationToken = default) { StopCalls++; return Task.CompletedTask; }
        public Task<MailboxHistoryPage> ReadHistoryAsync(MailboxProviderCall call, string history, string? page, CancellationToken cancellationToken = default) => Task.FromResult(History(page, cancellationToken));
        public Task<MailboxMessagePage> ListMessagesAsync(MailboxProviderCall call, IReadOnlyList<string> labels, DateTime? since, string? page, CancellationToken cancellationToken = default)
        { RequestedSince = since; return Task.FromResult(List(page, cancellationToken)); }
        public async Task<MailboxMessageReference> GetMessageAsync(MailboxProviderCall call, string id, CancellationToken cancellationToken = default)
        { if (BeforeGet is not null) await BeforeGet(); return Messages[id]; }
    }
}
