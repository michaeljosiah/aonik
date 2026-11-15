using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Connectors.Catalog;
using Aonik.Agents.Services.Tenant;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Agents.Connectors;

public sealed class ConnectorPackageInstallationTests
{
    [Fact]
    public async Task HttpInstallation_Should_RequireHumanPlatformReview_AndKeepTheExistingExtensionLifecycle()
    {
        using var scenario = new Scenario();
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        row.ApprovalState.Should().Be("Draft"); row.IsActive.Should().BeFalse();
        Func<Task> premature = () => scenario.Service.ActivateAsync(row.Id, new(row.ExpectedVersion));
        await premature.Should().ThrowAsync<InvalidOperationException>();
        row = await scenario.Service.SubmitAsync(row.Id, new(row.ExpectedVersion));
        scenario.PlatformAdmin = false;
        Func<Task> tenantApprove = () => scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        await tenantApprove.Should().ThrowAsync<PermissionDeniedException>();
        scenario.PlatformAdmin = true;
        row = await scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true, "Reviewed signed endpoint and read contract."));
        row.ApprovalState.Should().Be("Approved"); row.IsActive.Should().BeFalse();
        TenantHttpTool stored = await scenario.Database.TenantHttpTools.SingleAsync();
        stored.ReviewedByUserId.Should().Be(scenario.UserId); stored.RiskTier.Should().Be(TenantToolRiskTier.ReadOnly);
        row = await scenario.Service.ActivateAsync(row.Id, new(row.ExpectedVersion));
        row.IsActive.Should().BeTrue(); scenario.Remote.Calls.Should().BeEmpty("HTTP installation and review must not execute the endpoint");
        (await scenario.ImportAsync()).Single().Id.Should().Be(row.Id);
        (await scenario.Service.DisableAsync(row.Id, new(row.ExpectedVersion))).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task McpReview_Should_UseTheRealHttpTransport_AndRequireExactSignedInputAndOutputSchemas()
    {
        using var scenario = new Scenario(mcp: true);
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        row = await scenario.Service.SubmitAsync(row.Id, new(row.ExpectedVersion));
        scenario.Remote.ChangeSchema = true;
        Func<Task> changed = () => scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        await changed.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "ConnectorContractChanged");
        (await scenario.Database.TenantMcpServers.SingleAsync()).ApprovalState.Should().Be(TenantExtensionApprovalState.PendingPlatformReview);
        scenario.Remote.ChangeSchema = false;
        row = await scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        row.ApprovalState.Should().Be("Approved");
        scenario.Remote.Calls.Should().Contain("initialize").And.Contain("tools/list").And.NotContain("tools/call");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyTransitions_Should_NotBypassPackageReview_OrActivation(bool mcp)
    {
        using var scenario = new Scenario(mcp);
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        Func<Task> submit = async () => { if (mcp) await scenario.Mcp.SubmitAsync(row.Id); else await scenario.Http.SubmitAsync(row.Id); };
        Func<Task> review = async () => { if (mcp) await scenario.Mcp.ReviewAsync(row.Id, new(true)); else await scenario.Http.ReviewAsync(row.Id, new(true, RiskTier: "ReadOnly")); };
        Func<Task> activate = async () => { if (mcp) await scenario.Mcp.ActivateAsync(row.Id); else await scenario.Http.ActivateAsync(row.Id); };
        await submit.Should().ThrowAsync<InvalidOperationException>();
        await review.Should().ThrowAsync<InvalidOperationException>();
        await activate.Should().ThrowAsync<InvalidOperationException>();
        scenario.Remote.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("inactive")]
    [InlineData("anonymous")]
    [InlineData("egress")]
    [InlineData("digest")]
    public async Task Import_Should_FailClosed_BeforeCreatingAnExtension(string denied)
    {
        using var scenario = new Scenario();
        scenario.Denied = denied;
        Func<Task> import = () => scenario.ImportAsync();
        if (denied == "digest") await import.Should().ThrowAsync<InvalidOperationException>();
        else await import.Should().ThrowAsync<PermissionDeniedException>();
        scenario.Database.TenantHttpTools.Should().BeEmpty(); scenario.Database.TenantMcpServers.Should().BeEmpty();
        scenario.Remote.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CredentialRotation_Should_InvalidateApproval_AndRequireTheSignedHeader()
    {
        using var scenario = new Scenario(auth: "ApiKeyHeader");
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        row = await scenario.Service.SubmitAsync(row.Id, new(row.ExpectedVersion));
        row = await scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        row = await scenario.Service.ActivateAsync(row.Id, new(row.ExpectedVersion));
        TenantHttpTool stored = await scenario.Database.TenantHttpTools.SingleAsync();
        await scenario.Http.UpdateAsync(row.Id, new(stored.Name, stored.Description, stored.Method, stored.UrlTemplate,
            stored.ParameterSchemaJson, stored.AuthKind.ToString(), "rotated-private-secret", AuthHeaderName: "X-Wrong-Key"));
        stored.ApprovalState.Should().Be(TenantExtensionApprovalState.Draft); stored.IsActive.Should().BeFalse();
        row = (await scenario.Service.ListAsync()).Single();
        row = await scenario.Service.SubmitAsync(row.Id, new(row.ExpectedVersion));
        Func<Task> wrongHeader = () => scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        await wrongHeader.Should().ThrowAsync<InvalidOperationException>().WithMessage("ConnectorPackage.CredentialUnavailable");
        JsonSerializer.Serialize(row).Should().NotContain("private-secret");
    }

    [Fact]
    public async Task PackageBehaviorEdits_AndStaleCommands_Should_BeRejected()
    {
        using var scenario = new Scenario();
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        TenantHttpTool stored = await scenario.Database.TenantHttpTools.SingleAsync();
        Func<Task> edit = () => scenario.Http.UpdateAsync(row.Id, new(stored.Name, stored.Description, "POST", stored.UrlTemplate,
            stored.ParameterSchemaJson, stored.AuthKind.ToString()));
        await edit.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> stale = () => scenario.Service.SubmitAsync(row.Id, new("stale"));
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("ConnectorPackage.InstallationChanged");
        stored.ApprovalState.Should().Be(TenantExtensionApprovalState.Draft);
    }

    [Fact]
    public async Task SignedLowMutation_Should_StillRequireHighApproval()
    {
        using var scenario = new Scenario(mutation: true);
        ConnectorPackageInstallationItem row = (await scenario.ImportAsync()).Single();
        row = await scenario.Service.SubmitAsync(row.Id, new(row.ExpectedVersion));
        await scenario.Service.ReviewAsync(row.Id, new(row.ExpectedVersion, true));
        (await scenario.Database.TenantHttpTools.SingleAsync()).RiskTier.Should().Be(TenantToolRiskTier.High);
        scenario.Remote.Calls.Should().BeEmpty();
    }

    private sealed class Scenario : IDisposable
    {
        private readonly ConnectorPackageVerifierTests.Signer _signer = new();
        private readonly ConnectorPackageVerifierTests.Artifacts _files;
        private readonly TenantMcpConnectionCache _cache = new(NullLogger<TenantMcpConnectionCache>.Instance);
        private readonly CuratedConnectorCatalog _catalog;
        public AgentsDbContext Database { get; }
        public TenantMcpServerService Mcp { get; }
        public TenantHttpToolService Http { get; }
        public ConnectorPackageInstallationService Service { get; }
        public McpRemote Remote { get; }
        public Guid UserId { get; } = Guid.NewGuid();
        public bool PlatformAdmin { get; set; } = true;
        public string? Denied { get; set; }

        public Scenario(bool mcp = false, string auth = "None", bool mutation = false)
        {
            ConnectorPackageManifest manifest = ConnectorPackageVerifierTests.Manifest();
            if (mcp) manifest = manifest with { AdapterKind = "Mcp", Actions = [manifest.Actions[0] with { Endpoint = "https://example.test/mcp", RemoteToolName = "read_record", HttpMethod = null }] };
            if (mutation) manifest = manifest with { Actions = [manifest.Actions[0] with { MinimumRiskTier = "Low", HttpMethod = "POST" }] };
            manifest = manifest with { Authentication = new(auth, auth == "ApiKeyHeader" ? "X-Test-Key" : null) };
            _files = new(_signer, manifest); _catalog = new(_files.Options); Remote = new(manifest.Actions[0]);
            Guid tenantId = Guid.NewGuid(); var tenant = new TestTenantProvider(tenantId);
            var user = new Mock<ICurrentUserProvider>(); user.Setup(value => value.GetCurrentUserId()).Returns(() => Denied == "anonymous" ? null : UserId);
            user.Setup(value => value.TryGetCurrentUserId(out It.Ref<Guid>.IsAny)).Returns((out Guid id) => { id = UserId; return true; });
            var users = new Mock<IUserDirectoryReader>(); users.Setup(value => value.GetByIdsAsync(tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [new UserDirectoryItem(UserId, null, Denied == "inactive" ? "Inactive" : "Active")]);
            var permissions = new Mock<IPermissionService>(); permissions.Setup(value => value.HasPermissionAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Denied != "permission");
            var egress = new Mock<ITenantEgressAllowList>(); string? reason = null;
            egress.Setup(value => value.IsAllowed(It.IsAny<string>(), out reason)).Returns(() => Denied != "egress");
            var protector = new Mock<ITenantCredentialProtector>(); protector.Setup(value => value.Protect(It.IsAny<string>())).Returns((string json) => "protected:" + json);
            protector.Setup(value => value.Unprotect(It.IsAny<string>())).Returns((string encrypted) => encrypted[10..]);
            var clock = new Mock<IClock>(); clock.SetupGet(value => value.UtcNow).Returns(() => DateTime.UtcNow);
            Database = new(new DbContextOptionsBuilder<AgentsDbContext>().UseInMemoryDatabase("package-install-" + Guid.NewGuid()).Options, tenant, user.Object, clock.Object);
            Mcp = new(Database, tenant, user.Object, protector.Object, egress.Object, _cache, clock.Object);
            Http = new(Database, tenant, user.Object, protector.Object, egress.Object, clock.Object);
            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "PlatformAdmin")).ReturnsAsync(() => PlatformAdmin ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } };
            var clients = new Mock<IHttpClientFactory>(); clients.Setup(value => value.CreateClient("CuratedConnectorTool")).Returns(() => new HttpClient(Remote, disposeHandler: false));
            Service = new(Database, _catalog, Mcp, Http, egress.Object, tenant, user.Object, users.Object, permissions.Object,
                authorization.Object, context, new(protector.Object, egress.Object, clients.Object), protector.Object);
        }

        public Task<IReadOnlyList<ConnectorPackageInstallationItem>> ImportAsync() => Service.ImportAsync(new("synthetic-status", "1.0.0",
            Denied == "digest" ? new string('a', 64) : _catalog.Packages[0].Digest, "initial-private-secret"));
        public void Dispose() { Database.Dispose(); Remote.Dispose(); _files.Dispose(); _signer.Dispose(); }
    }

    private sealed class McpRemote(ConnectorPackageAction action) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public bool ChangeSchema { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post) return new(HttpStatusCode.MethodNotAllowed);
            using JsonDocument message = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string method = message.RootElement.GetProperty("method").GetString()!; Calls.Add(method);
            if (!message.RootElement.TryGetProperty("id", out JsonElement id)) return new(HttpStatusCode.Accepted);
            object result = method == "initialize" ? new { protocolVersion = message.RootElement.GetProperty("params").GetProperty("protocolVersion").GetString(), capabilities = new { tools = new { } }, serverInfo = new { name = "synthetic-fixture", version = "1.0.0" } }
                : new { tools = new[] { new { name = action.RemoteToolName, inputSchema = ChangeSchema ? JsonSerializer.SerializeToElement(new { type = "object", properties = new { unsafeField = new { type = "string" } } }) : action.InputSchema, outputSchema = action.OutputSchema } } };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }), Encoding.UTF8, "application/json") };
        }
    }
}
