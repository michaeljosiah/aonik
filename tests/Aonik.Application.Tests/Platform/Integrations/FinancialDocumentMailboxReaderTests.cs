using System.Security.Cryptography;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Platform.Integrations;

public class FinancialDocumentMailboxReaderTests
{
    [Fact]
    public async Task Read_Should_UseTheApprovedMachineAndAccount_ThroughTheExistingGateway_WithoutAHumanSession()
    {
        using var host = await Host.CreateAsync();

        var result = await host.Reader.ReadAsync(host.Binding.BindingId, 1, "message");

        FinancialDocumentAttachment source = result.Should().ContainSingle().Subject;
        source.Bytes.Should().Equal(host.Bytes); source.Source.AutomationIdentityId.Should().Be(host.Binding.AutomationIdentityId);
        source.Source.ProviderAccountId.Should().Be("synthetic-account"); source.Source.WorkflowGrantId.Should().Be(host.Binding.Connection.WorkflowGrantId);
        host.Scenario.Actions.Verify(value => value.InvokeAsync(It.Is<ConnectorProviderCall>(call => call.Action.Key == GmailActionKeys.GetMessage
            && call.SourceMetadataOnly && call.ProviderAccountId == "synthetic-account"), It.IsAny<CancellationToken>()), Times.Once);
        host.Scenario.Actions.Verify(value => value.InvokeAsync(It.Is<ConnectorProviderCall>(call => call.Action.Key == GmailActionKeys.GetAttachment
            && !call.SourceMetadataOnly), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("prefix")]
    [InlineData("labels")]
    [InlineData("old")]
    [InlineData("self")]
    public async Task Read_Should_ApplyCurrentSourceFilters_BeforeDownloadingAnyAttachment(string filter)
    {
        using var host = await Host.CreateAsync();
        host.Envelope = filter switch
        {
            "prefix" => host.Envelope with { Message = host.Envelope.Message with { Headers = new Dictionary<string, string> { ["Subject"] = "Unrelated private mail" } } },
            "labels" => host.Envelope with { LabelIds = ["OTHER"] },
            "old" => host.Envelope with { ReceivedAtUtc = host.Scenario.Clock.UtcNow.AddDays(-1) },
            _ => host.Envelope with { LabelIds = ["INBOX", "SENT"] },
        };

        (await host.Reader.ReadAsync(host.Binding.BindingId, 1, "message")).Should().BeEmpty();

        host.Scenario.Actions.Verify(value => value.InvokeAsync(It.Is<ConnectorProviderCall>(call => call.Action.Key == GmailActionKeys.GetAttachment), It.IsAny<CancellationToken>()), Times.Never);
        host.Content.Verify(value => value.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("changed-account")]
    [InlineData("wrong-reference")]
    [InlineData("changed-content")]
    public async Task Read_Should_DiscardSources_WhenAuthorityAccountOrPrivateContentChanges(string invalid)
    {
        using var host = await Host.CreateAsync();
        if (invalid == "revoked" || invalid == "changed-account") host.DuringAttachment = async () =>
        {
            if (invalid == "revoked") (await host.Database.ConnectorConnectionGrants.SingleAsync(grant => grant.GranteeType == "AutomationIdentity")).RevokedAt = host.Scenario.Clock.UtcNow;
            else (await host.Database.ConnectorConnections.SingleAsync()).ProviderAccountId = "another-google-account";
            await host.Database.SaveChangesAsync();
        };
        if (invalid == "wrong-reference") host.Stored = host.Stored with { ConnectionId = Guid.NewGuid() };
        if (invalid == "changed-content") host.Content.Setup(value => value.OpenAsync("protected-source", It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream("wrong"u8.ToArray()));

        Func<Task> read = () => host.Reader.ReadAsync(host.Binding.BindingId, 1, "message");

        if (invalid is "revoked" or "changed-account") await read.Should().ThrowAsync<ConnectorAccessException>();
        else await read.Should().ThrowAsync<ConnectorProviderException>();
    }

    [Fact]
    public async Task NewSubjects_Should_RequireAnExplicitUnexpiredAllSubjectsGrant_ForThePinnedVersion()
    {
        using var host = await Host.CreateAsync();
        var authority = host.Services.GetRequiredService<IAutomationIdentityAuthorization>();
        var actor = new WorkflowActor("AutomationIdentity", host.Binding.AutomationIdentityId, WorkflowVersionId: host.Binding.WorkflowVersionId);
        await authority.RequireAllSubjectsAsync(actor, "Document");
        var identity = await host.Database.AutomationIdentities.SingleAsync();
        identity.GrantsJson = ConnectorJson.Serialize(new[] { new AutomationWorkflowGrant(host.Binding.WorkflowVersionId, "Document", false, [Guid.NewGuid()],
            GmailActionKeys.ReadActions.Select(key => new AutomationActionRef(key, 1)).ToArray()) }); await host.Database.SaveChangesAsync();

        Func<Task> newSource = () => authority.RequireAllSubjectsAsync(actor, "Document");

        await newSource.Should().ThrowAsync<PermissionDeniedException>();
    }

    private sealed class Host : IDisposable
    {
        public ConnectorScenario Scenario { get; } public IServiceScope Scope { get; } public IServiceProvider Services => Scope.ServiceProvider;
        public PlatformDbContext Database => Services.GetRequiredService<PlatformDbContext>();
        public FinancialDocumentMailboxReader Reader => Services.GetRequiredService<FinancialDocumentMailboxReader>();
        public ConnectorSubscriptionBinding Binding { get; private set; } = null!;
        public Mock<IConnectorContentStore> Content { get; } = new(); public Mock<IWorkflowMailboxBindingReader> Bindings { get; } = new();
        public GmailSourceMessageResult Envelope { get; set; } = null!; public ConnectorStoredContent Stored { get; set; } = null!;
        public byte[] Bytes { get; } = "synthetic invoice"u8.ToArray(); public Func<Task>? DuringAttachment { get; set; }
        private Host()
        {
            Scenario = new(configure: services => { services.AddSingleton(Bindings.Object); services.AddSingleton(Content.Object); services.AddScoped<FinancialDocumentMailboxReader>(); });
            Scope = Scenario.Scope();
        }
        public static async Task<Host> CreateAsync()
        {
            var host = new Host(); await host.Scenario.InitializeAsync(); host.Scenario.Current.Authenticated = false;
            PlatformDbContext db = host.Database; Guid actorId = Guid.NewGuid(); Guid workflowId = Guid.NewGuid(); Guid versionId = Guid.NewGuid();
            db.Settings.Add(new Setting { TenantId = host.Scenario.TenantId, Scope = SettingScope.Tenant, Key = IntegrationSettingNames.GmailAllowWorkflowConnections, Value = "true" });
            db.AutomationIdentities.Add(new AutomationIdentity { Id = actorId, TenantId = host.Scenario.TenantId, Name = "Source intake", Status = "Active", ConfiguredByUserId = host.Scenario.Current.OwnerId,
                PermissionsJson = ConnectorJson.Serialize(new[] { "Integration.Use", "Workflow.Read", "Workflow.Run", "Document.Read" }),
                GrantsJson = ConnectorJson.Serialize(new[] { new AutomationWorkflowGrant(versionId, "Document", true, [], GmailActionKeys.ReadActions.Select(key => new AutomationActionRef(key, 1)).ToArray()) }) });
            var connection = new ConnectorConnection { TenantId = host.Scenario.TenantId, Ownership = "Tenant", ConnectorKey = "gmail", Status = "Active", ProviderAccountId = "synthetic-account",
                ProviderAccountLabel = "synthetic@example.test", GrantedScopesJson = ConnectorJson.Serialize(new[] { GmailActionKeys.ReadScope }), CredentialVersion = 1 };
            connection.ProtectedCredentials = host.Services.GetRequiredService<ConnectorCredentialProtector>().Protect(connection, host.Scenario.Tokens);
            db.ConnectorConnections.Add(connection);
            ConnectorActionRef[] actions = GmailActionKeys.ReadActions.Select(key => new ConnectorActionRef(key, 1)).ToArray();
            var grant = new ConnectorConnectionGrant { TenantId = host.Scenario.TenantId, ConnectionId = connection.Id, GranteeType = "Workflow", GranteeId = workflowId, AllowedActionsJson = ConnectorJson.Serialize(actions) };
            db.ConnectorConnectionGrants.AddRange(grant, new() { TenantId = host.Scenario.TenantId, ConnectionId = connection.Id, GranteeType = "AutomationIdentity", GranteeId = actorId, AllowedActionsJson = grant.AllowedActionsJson });
            await db.SaveChangesAsync();
            host.Binding = new(Guid.NewGuid(), 1, workflowId, versionId, actorId, new(connection.Id, grant.Id, actions), "Polling", 15, ["INBOX"], false, null,
                host.Scenario.Clock.UtcNow, "Document", "Qualification:");
            host.Bindings.Setup(value => value.RequireActiveAsync(host.Binding.BindingId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(() => host.Binding);
            string hash = Convert.ToHexStringLower(SHA256.HashData(host.Bytes));
            host.Stored = new("protected-source", connection.Id, "message", "attachment", "invoice.txt", "text/plain", host.Bytes.Length, hash, host.Scenario.Clock.UtcNow.AddDays(1));
            host.Content.Setup(value => value.Describe("protected-source")).Returns(() => host.Stored);
            host.Content.Setup(value => value.OpenAsync("protected-source", It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream(host.Bytes));
            host.Envelope = new(new("message", "thread", new Dictionary<string, string> { ["Subject"] = "Qualification: invoice" }, "", false,
                [new("attachment", "invoice.txt", "text/plain", host.Bytes.Length)]), host.Scenario.Clock.UtcNow, ["INBOX"]);
            host.Scenario.Actions.Setup(value => value.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
                .Returns(async (ConnectorProviderCall call, CancellationToken _) =>
                {
                    if (call.Action.Key == GmailActionKeys.GetMessage) return ConnectorJson.Element(host.Envelope);
                    if (host.DuringAttachment is not null) await host.DuringAttachment();
                    return ConnectorJson.Element(new GmailAttachmentResult("message", "attachment", "protected-source", "text/plain", host.Bytes.Length, hash));
                });
            return host;
        }
        public void Dispose() { Scope.Dispose(); Scenario.Dispose(); }
    }
}
