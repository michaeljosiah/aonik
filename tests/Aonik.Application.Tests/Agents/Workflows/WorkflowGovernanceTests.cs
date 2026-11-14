using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Agents.Approval;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowGovernanceTests
{
    [Fact]
    public async Task MasterOrchestrator_Should_ExposeAndInvokeTheGatedWorkflowTool_InItsRealModelLoop()
    {
        using var host = new Host();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ScriptedClient(Draft());
        AIAgent agent = await host.Orchestrator(client).GetAgentAsync(timeout.Token);

        await agent.RunAsync([new ChatMessage(ChatRole.User, "Prepare an invoice approval workflow draft")],
            session: null, cancellationToken: timeout.Token);

        client.OfferedTools.Should().Contain("aonik_get_workflow_capabilities").And.Contain("aonik_create_workflow")
            .And.Contain("aonik_request_publish_workflow");
        (await host.Db.Workflows.CountAsync()).Should().Be(0);
        ToolApprovalRequest pending = await host.Db.ToolApprovalRequests.SingleAsync();
        pending.Status.Should().Be(ToolApprovalRequestStatus.Pending);
        pending.PreviewJson.Should().Contain("invoice-route");
        (await host.Decide(pending.Id)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        await host.Invoke("aonik_create_workflow", new() { ["draft"] = Draft() });
        (await host.Db.Workflows.SingleAsync()).Slug.Should().Be("invoice-route");
    }

    [Fact]
    public async Task AuthoringTools_Should_CreateEditSimulateAndDelete_OnlyAfterExactHumanConfirmation()
    {
        using var host = new Host();
        WorkflowSaveRequest draft = Draft();

        ToolApprovalRequiredResult create = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft }));
        (await host.Workflows.GetBySlugAsync(draft.Slug)).Should().BeNull();
        create.Preview!.Before.Should().BeNull();
        create.Preview.After.Should().NotBeNull();
        (await host.Decide(create.ApprovalRequestId!.Value)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        WorkflowGraphResponse saved = As<WorkflowGraphResponse>(await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft }));
        saved.Nodes.Should().HaveCount(2);
        (await host.Db.WorkflowVersions.CountAsync()).Should().Be(1);

        WorkflowSaveRequest edit = draft with { Name = "Invoice approval route", ExpectedVersion = saved.ExpectedVersion };
        ToolApprovalRequiredResult update = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_update_workflow", new() { ["draft"] = edit }));
        update.Preview!.Before!.Value.GetProperty("name").GetString().Should().Be(draft.Name);
        update.Preview.After!.Value.GetProperty("name").GetString().Should().Be(edit.Name);
        await host.Decide(update.ApprovalRequestId!.Value);
        WorkflowGraphResponse changed = As<WorkflowGraphResponse>(await host.Invoke("aonik_update_workflow", new() { ["draft"] = edit }));
        changed.Nodes.Select(node => node.Id).Should().BeEquivalentTo(saved.Nodes.Select(node => node.Id));
        (await host.Db.WorkflowVersions.CountAsync()).Should().Be(2);
        As<WorkflowValidation>(await host.Invoke("aonik_validate_workflow", new() { ["slug"] = draft.Slug })).IsValid.Should().BeTrue();
        As<WorkflowSimulation>(await host.Invoke("aonik_simulate_workflow", new()
        {
            ["slug"] = draft.Slug, ["fixture"] = new WorkflowSimulationRequest(new Dictionary<string, JsonElement>())
        })).Status.Should().Be("Completed");

        var deletion = new Dictionary<string, object?> { ["slug"] = draft.Slug, ["expectedVersion"] = changed.ExpectedVersion };
        ToolApprovalRequiredResult delete = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_delete_workflow", deletion));
        delete.Preview!.After.Should().BeNull();
        await host.Decide(delete.ApprovalRequestId!.Value);
        As<bool>(await host.Invoke("aonik_delete_workflow", deletion)).Should().BeTrue();
        (await host.Workflows.GetBySlugAsync(draft.Slug)).Should().BeNull();
        (await host.Db.WorkflowVersions.CountAsync()).Should().Be(2, "deleting the unused draft preserves its history");
    }

    [Fact]
    public async Task ChangedArguments_Should_RequireNewConfirmation_WithoutConsumingTheOldApproval()
    {
        using var host = new Host();
        WorkflowSaveRequest draft = Draft();
        ToolApprovalRequiredResult pending = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft }));
        await host.Decide(pending.ApprovalRequestId!.Value);

        ToolApprovalRequiredResult altered = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft with { Name = "Altered" } }));

        altered.ApprovalRequestId!.Value.Should().NotBe(pending.ApprovalRequestId!.Value);
        (await host.Db.Workflows.CountAsync()).Should().Be(0);
        (await host.Db.ToolApprovalRequests.FindAsync(pending.ApprovalRequestId.Value))!.ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task Publication_Should_QueueWithoutActivating_AndAllowAnotherAuthorizedPublisherToDecide()
    {
        using var host = new Host();
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(Draft());
        WorkflowGovernanceCommand command = await host.Command(draft);

        ToolApprovalQueuedResult queued = As<ToolApprovalQueuedResult>(await host.Invoke("aonik_request_publish_workflow", new() { ["command"] = command }));

        (await host.Db.Workflows.SingleAsync()).PublishedVersionId.Should().BeNull();
        (await host.Db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed);
        queued.Preview!.After!.Value.GetProperty("slug").GetString().Should().Be(draft.Slug);
        host.Actor.Id = Host.Reviewer;
        (await host.Decide(queued.ApprovalRequestId)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        var published = await host.Db.WorkflowVersions.SingleAsync();
        published.PublishedByUserId.Should().Be(Host.Reviewer);
        published.DefinitionHash.Should().Be(command.DefinitionHash);
        (await host.Db.Workflows.SingleAsync()).PublishedVersionId.Should().Be(published.Id);
        Proposal proposal = await host.Db.Proposals.SingleAsync();
        proposal.Status.Should().Be(ProposalStatus.Applied);
        proposal.ExecutionResultJson.Should().NotBeNullOrEmpty();
        (await host.Governance.ApplyAsync(proposal.Id, false, CancellationToken.None)).AppliedResourceId.Should().Be(published.Id);
        (await host.Db.WorkflowVersions.CountAsync()).Should().Be(1, "handler replay returns its durable receipt");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedPublisher_Should_NotDecide_ThroughEitherApprovalPath(bool throughProposal)
    {
        using var host = new Host();
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(Draft());
        WorkflowGovernanceRequestResult pending = await host.Governance.RequestAsync(await host.Command(draft), false);
        host.Actor.Id = Host.Outsider;

        (await host.Proposals.GetByIdAsync(pending.ProposalId)).Should().BeNull("workflow governance data requires Workflow.Read");

        if (throughProposal)
        {
            Func<Task> decide = async () => { await host.Proposals.ApproveAsync(pending.ProposalId); };
            await decide.Should().ThrowAsync<PermissionDeniedException>();
            Func<Task> reject = async () => { await host.Proposals.DismissAsync(pending.ProposalId); };
            await reject.Should().ThrowAsync<PermissionDeniedException>();
        }
        else (await host.Decide(pending.ApprovalRequestId)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);

        (await host.Db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed);
        (await host.Db.Workflows.SingleAsync()).PublishedVersionId.Should().BeNull();
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("author")]
    [InlineData("expired")]
    public async Task Publication_Should_RefuseStaleDraftOrAuthority_AfterProposalApproval(string change)
    {
        using var host = new Host();
        WorkflowSaveRequest input = Draft();
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(input);
        WorkflowGovernanceRequestResult pending = await host.Governance.RequestAsync(await host.Command(draft), false);
        if (change == "draft") await host.Workflows.UpdateAsync(input with { Name = "Changed", ExpectedVersion = draft.ExpectedVersion });
        if (change == "author") host.Inactive.Add(Host.Author);
        if (change == "expired") host.Clock.UtcNow = host.Clock.UtcNow.AddHours(1);
        host.Actor.Id = Host.Reviewer;

        Func<Task> decide = async () => { await host.Proposals.ApproveAsync(pending.ProposalId); };

        await decide.Should().ThrowAsync<Exception>();
        (await host.Db.Workflows.SingleAsync()).PublishedVersionId.Should().BeNull();
        (await host.Db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Failed);
    }

    [Fact]
    public async Task RevokedDraftAuthority_Should_BlockConfirmationAndResubmission()
    {
        using var host = new Host();
        WorkflowSaveRequest draft = Draft();
        ToolApprovalRequiredResult pending = As<ToolApprovalRequiredResult>(await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft }));
        host.Denied.Add((Host.Author, "Workflow.Write"));

        (await host.Decide(pending.ApprovalRequestId!.Value)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        Func<Task> retry = async () => { await host.Invoke("aonik_create_workflow", new() { ["draft"] = draft }); };
        await retry.Should().ThrowAsync<PermissionDeniedException>();
        (await host.Db.Workflows.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RetirementAndRollback_Should_UseGovernance_AndKeepPublishedSnapshotsImmutable()
    {
        using var host = new Host();
        WorkflowSaveRequest input = Draft();
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(input);
        WorkflowGovernanceRequestResult first = await host.Governance.RequestAsync(await host.Command(draft), false);
        await host.Decide(first.ApprovalRequestId);
        WorkflowVersion original = await host.Db.WorkflowVersions.SingleAsync();
        string originalJson = original.DefinitionJson!;
        DateTime? originalPublishedAt = original.PublishedAt;
        WorkflowGraphResponse live = (await host.Workflows.GetBySlugAsync(input.Slug))!;
        await host.Workflows.UpdateAsync(input with { Name = "New draft", State = live.State, ExpectedVersion = live.ExpectedVersion });
        live = (await host.Workflows.GetBySlugAsync(input.Slug))!;
        WorkflowGovernanceRequestResult second = await host.Governance.RequestAsync(await host.Command(live), false);
        await host.Decide(second.ApprovalRequestId);
        live = (await host.Workflows.GetBySlugAsync(input.Slug))!;
        var rollback = new WorkflowGovernanceCommand(live.Slug, live.ExpectedVersion!, original.DefinitionHash!, live.PublishedVersionId, original.Id);
        await host.Decide((await host.Governance.RequestAsync(rollback, false)).ApprovalRequestId);
        original.DefinitionJson.Should().Be(originalJson);
        original.PublishedAt.Should().Be(originalPublishedAt);
        live = (await host.Workflows.GetBySlugAsync(input.Slug))!;
        WorkflowGovernanceRequestResult retirement = await host.Governance.RequestAsync(rollback with
        {
            ExpectedVersion = live.ExpectedVersion!, ExpectedPublishedVersionId = original.Id, TargetVersionId = null
        }, true);
        (await host.Db.Workflows.SingleAsync()).RetiredAt.Should().BeNull();
        await host.Decide(retirement.ApprovalRequestId);
        (await host.Db.Workflows.SingleAsync()).RetiredAt.Should().NotBeNull();
        Func<Task> remove = async () => { await host.Workflows.DeleteAsync(live.Slug, live.ExpectedVersion!); };
        await remove.Should().ThrowAsync<WorkflowConflictException>();
    }

    [Fact]
    public async Task Handler_Should_RequireAnActualApprovedProposal_NotCallerSuppliedPayload()
    {
        using var host = new Host();
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(Draft());
        WorkflowGovernanceRequestResult pending = await host.Governance.RequestAsync(await host.Command(draft), false);

        Func<Task> bypass = async () => { await host.Governance.ApplyAsync(pending.ProposalId, false, CancellationToken.None); };

        await bypass.Should().ThrowAsync<PermissionDeniedException>();
        (await host.Db.Workflows.SingleAsync()).PublishedVersionId.Should().BeNull();
    }

    [Fact]
    public async Task SeparatePublisherPolicy_Should_RejectSelfApproval_AtBothDecisionBoundaries()
    {
        using var host = new Host { RequireSeparatePublisher = true };
        WorkflowGraphResponse draft = await host.Workflows.CreateAsync(Draft());
        WorkflowGovernanceRequestResult pending = await host.Governance.RequestAsync(await host.Command(draft), false);

        (await host.Decide(pending.ApprovalRequestId)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        Func<Task> direct = async () => { await host.Proposals.ApproveAsync(pending.ProposalId); };
        await direct.Should().ThrowAsync<PermissionDeniedException>();
        (await host.Db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed);
        host.Actor.Id = Host.Reviewer;
        (await host.Decide(pending.ApprovalRequestId)).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
    }

    private static T As<T>(object? result) => result is T typed ? typed
        : JsonSerializer.SerializeToElement(result, WorkflowDefinitionCodec.JsonOptions).Deserialize<T>(WorkflowDefinitionCodec.JsonOptions)!;

    private static WorkflowSaveRequest Draft() => new("invoice-route", "Invoice route", "Invoice approval routing", "Draft", "v0.1", false,
        "#123456", null, [],
        [new("trigger", "trigger", "Start", "", "", 0, 0, "{}"), new("end", "end", "Done", "", "", 200, 0, "{}")],
        [new("trigger", "end", 0, "")], "Initial draft");

    private sealed class Host : IDisposable
    {
        public static readonly Guid Author = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid Reviewer = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid Outsider = Guid.Parse("10000000-0000-0000-0000-000000000003");
        private readonly ServiceProvider _provider;
        public Actor Actor { get; } = new();
        public TestClock Clock { get; } = new();
        public HashSet<Guid> Inactive { get; } = [];
        public HashSet<(Guid, string)> Denied { get; } = [];
        public bool RequireSeparatePublisher { get; set; }
        public AgentsDbContext Db => _provider.GetRequiredService<AgentsDbContext>();
        public IWorkflowService Workflows => _provider.GetRequiredService<IWorkflowService>();
        public WorkflowGovernanceService Governance => _provider.GetRequiredService<WorkflowGovernanceService>();
        public IProposalApprovalService Proposals => _provider.GetRequiredService<IProposalApprovalService>();

        public Host()
        {
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(value => value.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid user, string permission, CancellationToken _) => user != Outsider && !Denied.Contains((user, permission)));
            var users = new Mock<IUserDirectoryReader>();
            users.Setup(value => value.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<UserDirectoryItem>)ids
                    .Select(id => new UserDirectoryItem(id, null, Inactive.Contains(id) ? "Inactive" : "Active")).ToList());
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantProvider>(new Tenant());
            services.AddSingleton<ICurrentUserProvider>(Actor);
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton(permissions.Object);
            services.AddSingleton(users.Object);
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(value => value.GetTenantValueAsync(WorkflowSettingNames.RequireSeparatePublisher,
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => RequireSeparatePublisher ? "true" : "false");
            services.AddSingleton(settings.Object);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            string database = $"WorkflowGovernanceTests_{Guid.NewGuid()}";
            services.AddDbContext<AgentsDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddScoped<IWorkflowService, WorkflowService>();
            services.AddScoped<WorkflowAuthorization>();
            services.AddScoped<WorkflowDraftPreparation>();
            services.AddScoped<WorkflowGovernancePreparation>();
            services.AddScoped<WorkflowApprovalAuthority>();
            services.AddScoped<WorkflowGovernanceService>();
            services.AddScoped<IWorkflowGovernanceService>(provider => provider.GetRequiredService<WorkflowGovernanceService>());
            services.AddScoped<IAgentMutationGuard, WorkflowMutationGuard>();
            services.AddScoped<IToolApprovalRequestStore, ToolApprovalRequestStore>();
            services.AddScoped<IAgentProposalStore, AgentProposalStore>();
            services.AddScoped<IToolApprovalService, ToolApprovalService>();
            services.AddScoped<IProposalApprovalService, ProposalApprovalService>();
            services.AddScoped<IProposalApprovalPolicy, ProposalApprovalPolicy>();
            services.AddScoped<IProposalDispatcher, ProposalDispatcher>();
            services.AddSingleton(Mock.Of<IProposalRejectionDispatcher>());
            services.AddSingleton<IToolApprovalManifest, WorkflowToolApprovalManifest>();
            services.AddSingleton<IToolApprovalAuditSink, LoggingToolApprovalAuditSink>();
            services.AddSingleton<IToolApprovalGate, ToolApprovalGate>();
            foreach (string type in new[] { WorkflowToolApprovalManifest.PublishType, WorkflowToolApprovalManifest.RetireType })
                services.AddKeyedScoped<IProposalHandler>(type, (provider, key) => new WorkflowGovernanceProposalHandler((string)key!, provider.GetRequiredService<WorkflowGovernanceService>()));
            _provider = services.BuildServiceProvider();
        }

        public async Task<WorkflowGovernanceCommand> Command(WorkflowGraphResponse draft) => new(draft.Slug, draft.ExpectedVersion!,
            (await Workflows.ValidateAsync(draft.Slug))!.DefinitionHash, draft.PublishedVersionId);

        public async Task<object?> Invoke(string name, Dictionary<string, object?> args)
        {
            AIFunction tool = WorkflowTools.CreateAll(_provider).OfType<AIFunction>().Single(tool => tool.Name == name);
            return await tool.InvokeAsync(new AIFunctionArguments(args));
        }

        public Task<ToolApprovalDecisionResult> Decide(Guid id) => _provider.GetRequiredService<IToolApprovalService>()
            .DecideAsync(id, new(ToolApprovalDecisionType.Approve));

        public MasterOrchestratorService Orchestrator(IChatClient client)
        {
            IConfiguration configuration = _provider.GetRequiredService<IConfiguration>();
            return new([], new DescriptorModuleFilter(_provider.GetRequiredService<ITenantProvider>(), NullLogger<DescriptorModuleFilter>.Instance),
                Mock.Of<IMcpToolProvider>(), Mock.Of<IAgentConfigurationService>(), Mock.Of<IAiModelResolver>(), client,
                _provider, Actor, Mock.Of<IChatThreadService>(), Mock.Of<IChatThreadTitleGenerator>(),
                new InMemoryOrchestratorSessionStore(configuration, NullLogger<InMemoryOrchestratorSessionStore>.Instance),
                configuration, NullLogger<MasterOrchestratorService>.Instance);
        }
        public void Dispose() => _provider.Dispose();
    }

    private sealed class Actor : ICurrentUserProvider
    {
        public Guid Id { get; set; } = Host.Author;
        public Guid? GetCurrentUserId() => Id;
        public bool TryGetCurrentUserId(out Guid id) { id = Id; return true; }
    }

    private sealed class Tenant : ITenantProvider
    {
        private readonly Guid _id = Guid.NewGuid();
        public Guid GetCurrentTenantId() => _id;
        public bool TryGetCurrentTenantId(out Guid id) { id = _id; return true; }
    }

    private sealed class TestClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }

    private sealed class ScriptedClient(WorkflowSaveRequest draft) : IChatClient
    {
        private int _calls;
        public HashSet<string> OfferedTools { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            foreach (AITool tool in options?.Tools ?? []) OfferedTools.Add(tool.Name);
            ChatMessage reply = _calls++ == 0
                ? new(ChatRole.Assistant, [new FunctionCallContent("call_create_workflow", "aonik_create_workflow", new Dictionary<string, object?> { ["draft"] = draft })])
                : new(ChatRole.Assistant, "The draft is prepared for your confirmation.");
            return Task.FromResult(new ChatResponse([reply]));
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
