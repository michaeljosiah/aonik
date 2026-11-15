using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Connectors.Catalog;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Application.Tests.Platform.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Connectors;

public sealed class ConnectorPackageExecutionTests
{
    [Fact]
    public async Task NativeGmail_Should_PinTheReviewedRelease_AndRefuseAnUnavailablePinBeforeProviderUse()
    {
        await using var host = new Host(mcp: true, native: true);
        await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        IServiceProvider services = scope.ServiceProvider;
        var platform = services.GetRequiredService<PlatformDbContext>();
        platform.Settings.Add(new Setting { TenantId = host.Scenario.TenantId, Scope = SettingScope.Tenant,
            Key = IntegrationSettingNames.GmailAllowWorkflowConnections, Value = "true" });
        await platform.SaveChangesAsync();
        var requests = services.GetRequiredService<IConnectorConnectionRequests>();
        var consent = services.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(host.Scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "test-code", host.Scenario.Current.OwnerId);
        request = await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));

        var agents = services.GetRequiredService<AgentsDbContext>();
        var workflow = new Workflow { TenantId = host.Scenario.TenantId, Slug = "native-gmail", Name = "Native Gmail", State = "Draft" };
        agents.Workflows.Add(workflow);
        await agents.SaveChangesAsync();
        ConnectorConnection connection = await platform.ConnectorConnections.SingleAsync(row => row.Id == request.ConnectionId);
        var grants = services.GetRequiredService<ConnectorGrantService>();
        var action = new ConnectorActionRef(GmailActionKeys.Search, 1);
        await grants.IssueAsync(connection.Id, new("Workflow", workflow.Id, [action], Convert.ToBase64String(connection.RowVersion)));
        WorkflowConnectorBinding binding = (await grants.GetWorkflowBindingAsync(connection.Id, workflow.Id, action))!;
        ConnectorArtifactPin latest = binding.Artifact!;
        latest.PackageVersion.Should().Be("1.1.0");
        latest.PackageKey.Should().Be("native-gmail");

        var gateway = services.GetRequiredService<IConnectorService>();
        var catalog = services.GetRequiredService<CuratedConnectorCatalog>();
        VerifiedConnectorPackage prior = catalog.Require("native-gmail", "1.0.0");
        ConnectorArtifactPin older = prior.Pin(prior.Manifest.Actions.Single(entry => entry.Key == action.Key));
        var actor = new WorkflowActor("User", host.Scenario.Current.OwnerId, host.Scenario.Current.OwnerId);
        await gateway.ValidateWorkflowBindingAsync(workflow.Id, binding with { Artifact = older }, action, actor);
        await gateway.ValidateWorkflowBindingAsync(workflow.Id, binding with { Artifact = null }, action, actor);
        Func<Task> unavailable = () => gateway.ValidateWorkflowBindingAsync(workflow.Id,
            binding with { Artifact = older with { PackageVersion = "0.9.0" } }, action, actor);

        await unavailable.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "ConnectorContractChanged");
        await gateway.InvokeReadAsync(connection.Id, action, ConnectorJson.Element(new GmailSearchRequest("synthetic")));
        host.Scenario.Actions.Verify(provider => provider.InvokeAsync(It.Is<ConnectorProviderCall>(call =>
            call.ConnectionId == connection.Id && call.AccessToken == "synthetic-access"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_Should_UseTheOldReviewedReleaseWithoutAnHttpActor_AndRespectSecurityDisablement(bool disable)
    {
        await using var host = new Host(mcp: true);
        await host.InitializeAsync();
        ConnectorConnectionSummary oldAccount = await host.InstallAsync(Host.Read);
        await host.GrantAsync(oldAccount, Host.Read);
        (Guid runId, WorkflowConnectorBinding binding) = await host.StartAutomationAsync(oldAccount);
        ConnectorConnectionSummary newAccount = await host.InstallAsync(Host.Read, "1.1.0");
        newAccount.Id.Should().NotBe(oldAccount.Id);

        ConnectorPackageImpact preview = await host.PreviewAsync(oldAccount.Id, "1.1.0");
        preview.Workflows.Should().ContainSingle().Which.Slug.Should().Be("verified-curated-read");
        preview.Runs.Should().ContainSingle().Which.RunId.Should().Be(runId);
        preview.Target!.PackageVersion.Should().Be("1.1.0");
        preview.CurrentActions.Single(action => action.Action == Host.Read).ContractHash.Should().Be(binding.Artifact!.ActionContractHash);

        if (disable) await host.DisableAsync(oldAccount.Id);
        host.Scenario.Current.Authenticated = false;
        using IServiceScope worker = host.Scenario.Scope();
        var result = await worker.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(runId, 0);

        result.Status.Should().Be(disable ? WorkflowRunStatuses.WaitingForConnection : WorkflowRunStatuses.Completed, result.ErrorCode);
        host.Remote.Effects.Should().Equal(disable ? Array.Empty<string>() : new[] { "read" });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CuratedRead_Should_UseTheVerifiedInstallationGrantAndSchema_ThenStopAfterDisable(bool mcp)
    {
        await using var host = new Host(mcp);
        await host.InitializeAsync();
        ConnectorConnectionSummary account = await host.InstallAsync(Host.Read);
        using IServiceScope scope = host.Scenario.Scope();
        IConnectorService gateway = scope.ServiceProvider.GetRequiredService<IConnectorService>();
        var request = new CreateConnectionRequest("synthetic", "Tenant", [Host.Read], "Read synthetic invoice status", "curated-read-grant");
        JsonElement arguments = ConnectorJson.Element(new { id = "invoice-1" });

        Func<Task> ungranted = () => gateway.InvokeReadAsync(account.Id, Host.Read, arguments);
        await ungranted.Should().ThrowAsync<ConnectorAccessException>();
        host.Remote.Effects.Should().BeEmpty();

        var requests = scope.ServiceProvider.GetRequiredService<IConnectorConnectionRequests>();
        ConnectionRequestDetail access = await requests.CreateAsync(request);
        access.RequiredScopes.Should().BeEmpty();
        access.CanConnect.Should().BeFalse();
        host.Scenario.Current.Id = host.Scenario.ReviewerId;
        try { access = await requests.DecideAsync(access.Id, new(access.ExpectedVersion, "Approve", account.Id)); }
        finally { host.Scenario.Current.Id = host.Scenario.Current.OwnerId; }
        access.Status.Should().Be("Fulfilled");

        (await gateway.GetReadinessAsync("synthetic", Host.Read, account.Id)).Code.Should().Be("Ready");
        AIFunction read = host.Tool(scope, Host.Read);
        object? response = await read.InvokeAsync(new(ConnectorMutationArguments.ToolArguments(account.Id, arguments)));
        ConnectorJson.Element(response).GetProperty("status").GetString().Should().Be("ready");
        host.Remote.Effects.Should().Equal("read");

        Func<Task> invalid = () => gateway.InvokeReadAsync(account.Id, Host.Read, ConnectorJson.Element(new { id = "invoice-1", admin = true }));
        await invalid.Should().ThrowAsync<ArgumentException>();
        if (mcp)
        {
            host.Remote.ChangeSchema = true;
            await ungranted.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "ConnectorContractChanged");
        }
        await host.DisableAsync(account.Id);
        await ungranted.Should().ThrowAsync<ConnectorAccessException>();
        host.Remote.Effects.Should().Equal("read");
        host.Scenario.OAuth.Verify(provider => provider.RefreshAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CuratedMutation_Should_DispatchOnceThroughTheDurableHighProposal_AndHoldUncertainWrites(bool mcp, bool loseReply)
    {
        await using var host = new Host(mcp);
        await host.InitializeAsync();
        ConnectorConnectionSummary account = await host.InstallAsync(Host.Write);
        await host.GrantAsync(account, Host.Write);
        using IServiceScope scope = host.Scenario.Scope();
        AIFunction write = host.Tool(scope, Host.Write);
        JsonElement arguments = ConnectorJson.Element(new { idempotencyKey = "update-1", arguments = new { id = "invoice-1" } });
        var mutation = scope.ServiceProvider.GetRequiredService<IConnectorMutationService>();
        Func<Task> inline = () => mutation.ExecuteAsync(account.Id, Host.Write, arguments);
        await inline.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "DurableProposalRequired");

        await write.InvokeAsync(new(ConnectorMutationArguments.ToolArguments(account.Id, arguments)));
        await write.InvokeAsync(new(ConnectorMutationArguments.ToolArguments(account.Id, arguments)));
        var agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest approval = await agents.ToolApprovalRequests.AsNoTracking().SingleAsync();
        approval.RiskTier.Should().Be("High");
        (await agents.Proposals.CountAsync()).Should().Be(1);
        approval.PreviewJson.Should().Contain("invoice-1").And.Contain("serviceCredentialVersion");
        host.Remote.Effects.Should().BeEmpty();
        host.Remote.LoseReply = loseReply;

        var decisions = scope.ServiceProvider.GetRequiredService<IToolApprovalService>();
        if (loseReply)
        {
            Func<Task> approve = () => decisions.DecideAsync(approval.Id, new(ToolApprovalDecisionType.Approve));
            await approve.Should().ThrowAsync<ConnectorProviderException>();
            (await mutation.GetStatusAsync(approval.Id, reconcile: true))!.Status.Should().Be("NeedsAttention");
            (await agents.Proposals.AsNoTracking().SingleAsync()).Status.Should().Be(ProposalStatus.Failed);
        }
        else
        {
            (await decisions.DecideAsync(approval.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            (await mutation.GetStatusAsync(approval.Id))!.Result!.Value.GetProperty("status").GetString().Should().Be("ready");
        }

        await write.InvokeAsync(new(ConnectorMutationArguments.ToolArguments(account.Id, arguments)));
        host.Remote.Effects.Should().Equal("write");
    }

    [Fact]
    public async Task CredentialRotation_Should_InvalidateExistingPreview_EvenAfterFreshInstallationReview()
    {
        await using var host = new Host(mcp: false);
        await host.InitializeAsync();
        ConnectorConnectionSummary account = await host.InstallAsync(Host.Write);
        await host.GrantAsync(account, Host.Write);
        using IServiceScope scope = host.Scenario.Scope();
        JsonElement arguments = ConnectorJson.Element(new { idempotencyKey = "credential-rotation-1", arguments = new { id = "invoice-1" } });
        await host.Tool(scope, Host.Write).InvokeAsync(new(ConnectorMutationArguments.ToolArguments(account.Id, arguments)));
        var agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest approval = await agents.ToolApprovalRequests.AsNoTracking().SingleAsync();

        await host.RotateHttpCredentialAsync(account.Id);

        var decisions = scope.ServiceProvider.GetRequiredService<IToolApprovalService>();
        (await decisions.DecideAsync(approval.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        host.Remote.Effects.Should().BeEmpty();
    }

    private sealed class Host : IAsyncDisposable
    {
        public static ConnectorActionRef Read { get; } = new("synthetic.records.get", 1);
        public static ConnectorActionRef Write { get; } = new("synthetic.records.update", 1);
        public ConnectorScenario Scenario { get; }
        public RemoteServer Remote { get; }
        private Guid SubjectId { get; } = Guid.NewGuid();
        private readonly ConnectorPackageVerifierTests.Signer _signer = new();
        private readonly ConnectorPackageVerifierTests.Artifacts _artifacts;
        private readonly Dictionary<Guid, ConnectorPackageInstallationItem> _installations = [];
        private readonly bool _mcp;

        public Host(bool mcp, bool native = false)
        {
            _mcp = mcp;
            ConnectorPackageManifest original = ConnectorPackageVerifierTests.Manifest();
            ConnectorPackageAction read = original.Actions[0];
            ConnectorPackageAction write = read with { Key = Write.Key, DisplayName = "Update record", MinimumRiskTier = "Medium", HttpMethod = "POST" };
            if (mcp)
            {
                read = read with { Endpoint = "https://example.test/mcp", RemoteToolName = "get_record", HttpMethod = null };
                write = write with { Endpoint = read.Endpoint, RemoteToolName = "get_and_update_record", HttpMethod = null };
            }
            ConnectorPackageManifest manifest = original with { AdapterKind = mcp ? "Mcp" : "Http", Actions = [read, write],
                Authentication = new("ApiKeyHeader", "X-Test-Key") };
            ConnectorPackageManifest installed = native ? ConnectorPackageVerifierTests.NativeGmailManifest() : manifest;
            _artifacts = new(_signer, installed, installed with { PackageVersion = "1.1.0" });
            Remote = new RemoteServer(manifest.Actions);
            Scenario = new ConnectorScenario(configure: services =>
            {
                services.AddLogging();
                services.AddScoped<IConnectorService>(provider => provider.GetRequiredService<ConnectorService>());
                services.AddScoped<IConnectorMutationService, ConnectorMutationService>();
                var settings = new Mock<ITenantSettingStore>();
                settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("true");
                services.AddSingleton(settings.Object);
                var facts = new Mock<IWorkflowFactsProvider>();
                facts.SetupGet(provider => provider.SubjectType).Returns("Invoice");
                facts.SetupGet(provider => provider.Schema).Returns("InvoiceFacts@1");
                facts.Setup(provider => provider.LoadAsync(SubjectId, It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WorkflowFactsSnapshot("InvoiceFacts@1", new Dictionary<string, JsonElement> { ["invoice.id"] = ConnectorJson.Element(SubjectId) }, "version-1"));
                services.AddSingleton(facts.Object);
                services.AddSingleton(Mock.Of<IChatParticipantDirectory>());
                services.AddSingleton(Mock.Of<IRecurrenceScheduleCalculator>());
                var policy = new Mock<IAuthorizationService>();
                policy.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "PlatformAdmin")).ReturnsAsync(AuthorizationResult.Success());
                services.AddSingleton(policy.Object);
                string name = "CuratedPackage_" + Guid.NewGuid();
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseInMemoryDatabase"] = "true",
                    ["InMemoryDatabaseName"] = name,
                    ["Agents:TenantExtensions:AllowedEgressHosts:0"] = "example.test",
                    ["Agents:ConnectorCatalog:ArtifactDirectory"] = _artifacts.Options.ArtifactDirectory,
                    ["Agents:ConnectorCatalog:ArtifactFiles:0"] = _artifacts.Options.ArtifactFiles[0],
                    ["Agents:ConnectorCatalog:ArtifactFiles:1"] = _artifacts.Options.ArtifactFiles[1],
                    ["Agents:ConnectorCatalog:TrustedPublishers:0:PublisherId"] = "synthetic-publisher",
                    ["Agents:ConnectorCatalog:TrustedPublishers:0:KeyId"] = "test-key",
                    ["Agents:ConnectorCatalog:TrustedPublishers:0:PublicKeyPem"] = _signer.Trust.TrustedPublishers[0].PublicKeyPem
                }).Build();
                Aonik.Agents.AgentsModule.ConfigureServices(services, configuration);
                services.AddHttpClient("CuratedConnectorTool").ConfigurePrimaryHttpMessageHandler(() => Remote);
            });
        }

        public async Task InitializeAsync()
        {
            await Scenario.InitializeAsync();
            Scenario.Allowed.Add((Scenario.Current.OwnerId, "Integration.Manage"));
            using IServiceScope scope = Scenario.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Settings.Add(new Setting { TenantId = Scenario.TenantId, Scope = SettingScope.Tenant, Key = IntegrationSettingNames.CuratedEnabled, Value = "true" });
            database.Settings.Add(new Setting { TenantId = Scenario.TenantId, Scope = SettingScope.Tenant, Key = IntegrationSettingNames.CuratedAllowWorkflowConnections, Value = "true" });
            await database.SaveChangesAsync();
        }

        public async Task<ConnectorConnectionSummary> InstallAsync(ConnectorActionRef action, string release = "1.0.0")
        {
            using IServiceScope scope = Scenario.Scope();
            IServiceProvider services = scope.ServiceProvider;
            var catalog = services.GetRequiredService<IConnectorPackageCatalog>();
            var lifecycle = services.GetRequiredService<IConnectorPackageInstallationService>();
            var gateway = services.GetRequiredService<IConnectorService>();
            var context = services.GetRequiredService<IHttpContextAccessor>();
            context.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("fixture")) };
            try
            {
                ConnectorPackageDetail detail = (await catalog.GetAsync("synthetic-status", release))!;
                ConnectorPackageInstallationItem row = (await lifecycle.ImportAsync(new(detail.Package.PackageKey, release, detail.Package.Digest, "fixture-service-key")))
                    .Single(row => _mcp || row.Action == action);
                row = await lifecycle.SubmitAsync(row.Id, new(row.ExpectedVersion));
                row = await lifecycle.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
                row = await lifecycle.ActivateAsync(row.Id, new(row.ExpectedVersion));
                string providerId = (_mcp ? "mcp:" : "http:") + row.Id.ToString("N");
                ConnectorConnectionSummary account = await gateway.ProvisionServiceAccountAsync("synthetic", providerId, "Synthetic service");
                (await gateway.ProvisionServiceAccountAsync("synthetic", providerId, "Synthetic service")).Id.Should().Be(account.Id);
                _installations.Add(account.Id, row);
                return account;
            }
            finally { context.HttpContext = null; }
        }

        public async Task GrantAsync(ConnectorConnectionSummary account, ConnectorActionRef action)
        {
            using IServiceScope scope = Scenario.Scope();
            await scope.ServiceProvider.GetRequiredService<ConnectorGrantService>().IssueAsync(account.Id, new("User", Scenario.Current.OwnerId, [action], account.ExpectedVersion));
        }

        public async Task<(Guid, WorkflowConnectorBinding)> StartAutomationAsync(ConnectorConnectionSummary account)
        {
            using IServiceScope scope = Scenario.Scope();
            IServiceProvider services = scope.ServiceProvider;
            var database = services.GetRequiredService<AgentsDbContext>();
            var platform = services.GetRequiredService<PlatformDbContext>();
            var grants = services.GetRequiredService<ConnectorGrantService>();
            var workflow = new Workflow { TenantId = Scenario.TenantId, Slug = "verified-curated-read", Name = "Verified curated read", State = "Draft" };
            database.Workflows.Add(workflow);
            await database.SaveChangesAsync();
            ConnectorConnection connection = await platform.ConnectorConnections.SingleAsync(row => row.Id == account.Id);
            await grants.IssueAsync(account.Id, new("Workflow", workflow.Id, [Read], Convert.ToBase64String(connection.RowVersion)));
            WorkflowConnectorBinding binding = (await grants.GetWorkflowBindingAsync(account.Id, workflow.Id, Read))!;
            binding.Artifact!.PackageVersion.Should().Be("1.0.0");
            var parameters = new WorkflowToolParameters(Read.Key, Read.Version, new Dictionary<string, WorkflowInputBinding>
            { ["id"] = new(Literal: ConnectorJson.Element("invoice-1")) }, Connection: binding);
            var definition = new WorkflowDefinition(1, "Workflow", workflow.Slug, workflow.Name, "",
                [new("start", "trigger", "Start", ConnectorJson.Element(new { })), new("read", "tool", "Read record", ConnectorJson.Element(parameters)),
                    new("done", "end", "Done", ConnectorJson.Element(new { }))],
                [new("start-read", "start", "read", 0, null), new("read-done", "read", "done", 0, null)]);
            string json = WorkflowDefinitionCodec.Serialize(definition);
            var version = new WorkflowVersion { TenantId = Scenario.TenantId, WorkflowId = workflow.Id, DefinitionJson = json,
                DefinitionHash = WorkflowDefinitionCodec.Hash(json), SchemaVersion = 1, Tag = "v1", PublishedAt = Scenario.Clock.UtcNow, PublishedByUserId = Scenario.Current.OwnerId };
            database.WorkflowVersions.Add(version);
            workflow.PublishedVersionId = version.Id;
            workflow.State = "Active";
            await database.SaveChangesAsync();
            var identity = new AutomationIdentity { TenantId = Scenario.TenantId, Name = "Fixture worker", ConfiguredByUserId = Scenario.Current.OwnerId,
                AccountableUserId = Scenario.Current.OwnerId, PermissionsJson = ConnectorJson.Serialize(new[] { "Workflow.Read", "Workflow.Run", "Integration.Use", "Integration.Read", "Invoice.Read" }),
                GrantsJson = ConnectorJson.Serialize(new[] { new AutomationWorkflowGrant(version.Id, "Invoice", false, [SubjectId], [new(Read.Key, Read.Version)]) }) };
            platform.AutomationIdentities.Add(identity);
            await platform.SaveChangesAsync();
            await grants.IssueAsync(account.Id, new("AutomationIdentity", identity.Id, [Read], Convert.ToBase64String(connection.RowVersion)));
            Guid runId = await services.GetRequiredService<IWorkflowAutomationAdmission>().AdmitAsync(identity.Id, version.Id, "Invoice", SubjectId, "install-compatibility");
            return (runId, binding);
        }

        public async Task<ConnectorPackageImpact> PreviewAsync(Guid accountId, string version)
        {
            using IServiceScope scope = Scenario.Scope();
            return await scope.ServiceProvider.GetRequiredService<IConnectorPackageInstallationService>().PreviewAsync(_installations[accountId].Id, version);
        }

        public AIFunction Tool(IServiceScope scope, ConnectorActionRef action)
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CuratedConnectorCatalog>();
            return ConnectorTools.CreateAll(scope.ServiceProvider).OfType<AIFunction>().Single(tool => tool.Name == catalog.FindAction(action)!.Descriptor.ToolName);
        }

        public async Task DisableAsync(Guid accountId)
        {
            using IServiceScope scope = Scenario.Scope();
            ConnectorPackageInstallationItem row = _installations[accountId];
            await scope.ServiceProvider.GetRequiredService<IConnectorPackageInstallationService>().DisableAsync(row.Id, new(row.ExpectedVersion));
        }

        public async Task RotateHttpCredentialAsync(Guid accountId)
        {
            using IServiceScope scope = Scenario.Scope();
            IServiceProvider services = scope.ServiceProvider;
            var context = services.GetRequiredService<IHttpContextAccessor>();
            context.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("fixture")) };
            try
            {
                ConnectorPackageInstallationItem row = _installations[accountId];
                var database = services.GetRequiredService<AgentsDbContext>();
                TenantHttpTool tool = await database.TenantHttpTools.SingleAsync(tool => tool.Id == row.Id);
                var definitions = services.GetRequiredService<Aonik.Agents.Services.Tenant.ITenantHttpToolService>();
                await definitions.UpdateAsync(row.Id, new(tool.Name, tool.Description, tool.Method, tool.UrlTemplate, tool.ParameterSchemaJson, tool.AuthKind.ToString(),
                    "rotated-service-key", AuthHeaderName: "X-Test-Key"));
                var lifecycle = services.GetRequiredService<IConnectorPackageInstallationService>();
                row = (await lifecycle.ListAsync()).Single(item => item.Id == row.Id);
                row = await lifecycle.SubmitAsync(row.Id, new(row.ExpectedVersion));
                row = await lifecycle.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
                await lifecycle.ActivateAsync(row.Id, new(row.ExpectedVersion));
            }
            finally { context.HttpContext = null; }
        }

        public async ValueTask DisposeAsync()
        {
            await Scenario.DisposeAsync();
            _artifacts.Dispose();
            _signer.Dispose();
        }
    }

    private sealed class RemoteServer(IReadOnlyList<ConnectorPackageAction> actions) : HttpMessageHandler
    {
        public List<string> Effects { get; } = [];
        public bool LoseReply { get; set; }
        public bool ChangeSchema { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != "/mcp")
            {
                request.Headers.GetValues("X-Test-Key").Single().Should().Be("fixture-service-key");
                request.RequestUri.AbsolutePath.Should().Be("/records/invoice-1");
                bool write = request.Method == HttpMethod.Post;
                Effects.Add(write ? "write" : "read");
                if (write && LoseReply) throw new HttpRequestException("synthetic lost reply");
                return Json(new { status = "ready" });
            }
            if (request.Method != HttpMethod.Post) return new(HttpStatusCode.MethodNotAllowed);
            request.Headers.GetValues("X-Test-Key").Single().Should().Be("fixture-service-key");
            using JsonDocument payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            JsonElement message = payload.RootElement;
            string method = message.GetProperty("method").GetString()!;
            if (!message.TryGetProperty("id", out JsonElement id)) return new(HttpStatusCode.Accepted);
            object result;
            if (method == "initialize")
                result = new { protocolVersion = message.GetProperty("params").GetProperty("protocolVersion").GetString(),
                    capabilities = new { tools = new { } }, serverInfo = new { name = "synthetic-mcp", version = "1.0.0" } };
            else if (method == "tools/list")
                result = new { tools = actions.Select(action => new { name = action.RemoteToolName,
                    inputSchema = ChangeSchema ? ConnectorJson.Element(new { type = "object" }) : action.InputSchema, outputSchema = action.OutputSchema }).ToArray() };
            else
            {
                method.Should().Be("tools/call");
                JsonElement parameters = message.GetProperty("params");
                parameters.GetProperty("arguments").GetProperty("id").GetString().Should().Be("invoice-1");
                bool write = parameters.GetProperty("name").GetString() == "get_and_update_record";
                Effects.Add(write ? "write" : "read");
                if (write && LoseReply) throw new HttpRequestException("synthetic lost reply");
                result = new { content = Array.Empty<object>(), structuredContent = new { status = "ready" } };
            }
            return Json(new { jsonrpc = "2.0", id, result });
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}
