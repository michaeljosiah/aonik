using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Application.Tests.Platform.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public class ConnectorWorkflowTests
{
    [Fact]
    public async Task RevokedBinding_Should_PauseAtItsCheckpoint_AndRequireExplicitResumeOfTheSameAccount()
    {
        using var host = new Host();
        Guid runId = await host.StartAsync();
        using (var scope = host.Scenario.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.ConnectorConnectionGrants.SingleAsync(grant => grant.Id == host.WorkflowGrantId)).RevokedAt = host.Scenario.Clock.UtcNow;
            await db.SaveChangesAsync();
        }

        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForConnection);
        host.Scenario.Actions.Verify(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()), Times.Never);
        using (var scope = host.Scenario.Scope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
            WorkflowRunDetail waiting = (await runs.GetAsync(runId))!;
            Func<Task> premature = () => runs.ResumeAsync(runId, new(waiting.ExpectedVersion));
            await premature.Should().ThrowAsync<ConnectorAccessException>();
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var grant = await db.ConnectorConnectionGrants.SingleAsync(grant => grant.Id == host.WorkflowGrantId);
            await scope.ServiceProvider.GetRequiredService<ConnectorGrantService>().IssueAsync(host.ConnectionId,
                new("Workflow", host.WorkflowId, [new(GmailActionKeys.Search, 1)], Convert.ToBase64String((await db.ConnectorConnections.SingleAsync()).RowVersion),
                    grant.Id, Convert.ToBase64String(grant.RowVersion)));
            (await runs.GetAsync(runId))!.Status.Should().Be(WorkflowRunStatuses.WaitingForConnection);
            await runs.ResumeAsync(runId, new(waiting.ExpectedVersion));
        }

        (await host.ContinueAsync(runId, 1)).Status.Should().Be(WorkflowRunStatuses.Completed);
        host.Scenario.Actions.Verify(provider => provider.InvokeAsync(It.Is<ConnectorProviderCall>(call => call.ConnectionId == host.ConnectionId), It.IsAny<CancellationToken>()), Times.Once);
        using var final = host.Scenario.Scope();
        var agents = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await agents.WorkflowStepRuns.CountAsync(step => step.ActionKey == GmailActionKeys.Search)).Should().Be(1);
        (await agents.WorkflowRuns.SingleAsync()).WorkflowVersionId.Should().Be(host.VersionId);
        (await agents.WorkflowCheckpoints.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConnectorAdapter_Should_RefuseInventedInvocationOutsideTheActualLeasedStep()
    {
        using var host = new Host();
        Guid runId = await host.StartAsync();
        using var scope = host.Scenario.Scope();
        var gateway = scope.ServiceProvider.GetRequiredService<IConnectorService>();

        Func<Task> direct = () => gateway.InvokeWorkflowReadAsync(runId, Guid.NewGuid(), host.ConnectionId,
            new(GmailActionKeys.Search, 1), ConnectorJson.Element(new GmailSearchRequest("invoice")));

        await direct.Should().ThrowAsync<Aonik.SharedKernel.Abstractions.PermissionDeniedException>();
        host.Scenario.Actions.Verify(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Host : IDisposable
    {
        public ConnectorScenario Scenario { get; }
        public Guid WorkflowId { get; } = Guid.NewGuid();
        public Guid SubjectId { get; } = Guid.NewGuid();
        public Guid ConnectionId { get; private set; }
        public Guid WorkflowGrantId { get; private set; }
        public Guid VersionId { get; private set; }
        public Host()
        {
            string databaseName = $"ConnectorWorkflow_{Guid.NewGuid()}";
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("true");
            var facts = new Mock<IWorkflowFactsProvider>();
            facts.SetupGet(provider => provider.SubjectType).Returns("Invoice");
            facts.SetupGet(provider => provider.Schema).Returns("InvoiceFacts@1");
            facts.Setup(provider => provider.LoadAsync(SubjectId, It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowFactsSnapshot("InvoiceFacts@1", new Dictionary<string, JsonElement> { ["invoice.id"] = ConnectorJson.Element(SubjectId) }, "version-1"));
            Scenario = new(configure: services =>
            {
                services.AddLogging();
                services.AddDbContext<AgentsDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddSingleton(settings.Object); services.AddSingleton(facts.Object);
                services.AddSingleton(Mock.Of<IToolApprovalService>());
                services.AddScoped<WorkflowAuthorization>(); services.AddScoped<WorkflowExecutionAuthorization>();
                services.AddScoped<WorkflowInvocationContext>(); services.AddScoped<WorkflowActionRegistry>();
                services.AddScoped<WorkflowStepExecutionService>(); services.AddScoped<IGraphWorkflowRunner, GraphWorkflowRunner>();
                services.AddScoped<IWorkflowRunService, WorkflowRunService>();
                services.AddScoped<IWorkflowConnectorContextReader, WorkflowConnectorContextReader>();
                services.AddScoped<IConnectorService>(provider => provider.GetRequiredService<ConnectorService>());
                services.AddScoped<IWorkflowActionHandler>(provider => new ConnectorWorkflowAction(
                    provider.GetRequiredService<IConnectorDescriptor>().Descriptor.Actions.Single(action => action.Key == GmailActionKeys.Search),
                    provider.GetRequiredService<IConnectorService>(), provider.GetServices<IConnectorActionProvider>(), provider.GetRequiredService<WorkflowInvocationContext>()));
            });
        }
        public async Task<Guid> StartAsync()
        {
            await Scenario.InitializeAsync();
            using var scope = Scenario.Scope();
            var services = scope.ServiceProvider;
            var platform = services.GetRequiredService<PlatformDbContext>();
            platform.Settings.Add(new Setting { Key = IntegrationSettingNames.GmailAllowWorkflowConnections, Value = "true", Scope = SettingScope.Tenant, TenantId = Scenario.TenantId });
            await platform.SaveChangesAsync();
            var requests = services.GetRequiredService<ConnectorConnectionRequestService>();
            var consent = services.GetRequiredService<ConnectorConsentService>();
            var request = await requests.CreateAsync(Scenario.Request());
            var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
            await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", Scenario.Current.OwnerId);
            var connected = await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));
            ConnectionId = connected.ConnectionId!.Value;
            var agents = services.GetRequiredService<AgentsDbContext>();
            var workflow = new Workflow { Id = WorkflowId, TenantId = Scenario.TenantId, Slug = "gmail-invoice-evidence", Name = "Invoice evidence", State = "Draft" };
            agents.Workflows.Add(workflow); await agents.SaveChangesAsync();
            var connection = await platform.ConnectorConnections.SingleAsync();
            var grant = await services.GetRequiredService<ConnectorGrantService>().IssueAsync(ConnectionId,
                new("Workflow", WorkflowId, [new(GmailActionKeys.Search, 1)], Convert.ToBase64String(connection.RowVersion)));
            WorkflowGrantId = grant.Id;
            var parameters = new WorkflowToolParameters(GmailActionKeys.Search, 1,
                new Dictionary<string, WorkflowInputBinding> { ["query"] = new(Literal: ConnectorJson.Element("subject:invoice")) },
                Connection: new(ConnectionId, grant.Id, [new(GmailActionKeys.Search, 1)]));
            var definition = new WorkflowDefinition(1, "Workflow", workflow.Slug, workflow.Name, "",
                [new("start", "trigger", "Start", ConnectorJson.Element(new { })), new("mail", "tool", "Find evidence", ConnectorJson.Element(parameters)), new("done", "end", "Done", ConnectorJson.Element(new { }))],
                [new("start-mail", "start", "mail", 0, null), new("mail-done", "mail", "done", 0, null)]);
            string json = WorkflowDefinitionCodec.Serialize(definition);
            var version = new WorkflowVersion { TenantId = Scenario.TenantId, WorkflowId = workflow.Id, Tag = "v1", SchemaVersion = 1,
                DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json), PublishedAt = Scenario.Clock.UtcNow, PublishedByUserId = Scenario.Current.OwnerId };
            agents.WorkflowVersions.Add(version); workflow.State = "Active"; workflow.PublishedVersionId = version.Id; VersionId = version.Id;
            await agents.SaveChangesAsync();
            return (await services.GetRequiredService<IWorkflowRunService>().StartAsync(workflow.Slug, new(version.Id, "Invoice", SubjectId, "mail-run-1"))).Id;
        }
        public async Task<GraphWorkflowContinuation> ContinueAsync(Guid id, int continuation = 0)
        { using var scope = Scenario.Scope(); return await scope.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(id, continuation); }
        public void Dispose() => Scenario.Dispose();
    }
}
