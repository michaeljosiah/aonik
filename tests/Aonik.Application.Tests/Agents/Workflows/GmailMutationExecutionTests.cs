using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Connectors;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Application.Tests.Platform.Integrations;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Agents.Approval;

namespace Aonik.Application.Tests.Agents.Workflows;

public class GmailMutationExecutionTests
{
    [Theory]
    [InlineData(GmailActionKeys.Archive, false)]
    [InlineData(GmailActionKeys.SendMessage, true)]
    public async Task NativeWorkflow_Should_RequireTheNamedMailboxReviewer_AndExecuteThroughTheSameClaim(string action, bool high)
    {
        using var host = new Host(); await host.InitializeAsync();
        Guid runId = await host.StartWorkflowAsync(action);
        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope scope = host.Scenario.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync(); request.PreviewJson.Should().Contain("reviewed");
        host.Writes.Should().Be(0);
        host.Scenario.Current.Id = host.Scenario.ReviewerId;

        (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        host.Writes.Should().Be(high ? 1 : 0);
        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);

        host.Writes.Should().Be(1);
        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await db.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.RunId == runId && step.ActionKey == action)).ResultJson.Should().Contain("message-1");
        host.Writes.Should().Be(1);
    }

    [Fact]
    public async Task WorkflowRoleWithoutMailboxGrant_Should_NotSeePrivatePreviewOrApproveTheSend()
    {
        using var host = new Host(); await host.InitializeAsync();
        Guid runId = await host.StartWorkflowAsync(GmailActionKeys.SendMessage, grantReviewer: false);
        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        host.Scenario.Current.Id = host.Scenario.ReviewerId;
        using IServiceScope scope = host.Scenario.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();

        WorkflowRunDetail detail = (await scope.ServiceProvider.GetRequiredService<IWorkflowRunService>().GetAsync(runId))!;

        detail.Steps.Single(step => step.ActionKey == GmailActionKeys.SendMessage).Approval!.Preview.Should().BeNull();
        detail.Steps.Single(step => step.ActionKey == GmailActionKeys.SendMessage).Approval!.CanDecide.Should().BeFalse();
        (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        host.Writes.Should().Be(0);
    }

    [Fact]
    public async Task FailedWorkflowSend_Should_ReconcileItsReceiptOnExplicitResume_WithoutRepeatingTheFailedProposal()
    {
        using var host = new Host(); await host.InitializeAsync();
        Guid runId = await host.StartWorkflowAsync(GmailActionKeys.SendMessage);
        await host.ContinueAsync(runId);
        host.Scenario.Actions.Setup(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .Returns((ConnectorProviderCall _, CancellationToken _) => { host.Writes++; throw new ConnectorProviderException("ProviderUnavailable", true); });
        host.Scenario.Current.Id = host.Scenario.ReviewerId;
        using IServiceScope scope = host.Scenario.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        await ((Func<Task>)(() => scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))))
            .Should().ThrowAsync<ConnectorProviderException>();
        (await host.ContinueAsync(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        host.Writes.Should().Be(1);
        host.Scenario.Actions.Setup(provider => provider.ReconcileAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectorProviderCall call, CancellationToken _) => new(Host.Receipt(call, reconciled: true)));
        IWorkflowRunService runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
        WorkflowRunDetail held = (await runs.GetAsync(runId))!;
        await runs.ResumeAsync(runId, new(held.ExpectedVersion));

        GraphWorkflowContinuation recovered = await host.ContinueAsync(runId, 1);

        recovered.Status.Should().Be(WorkflowRunStatuses.Completed, recovered.ErrorCode);
        (await db.Proposals.AsNoTracking().SingleAsync()).Status.Should().Be(ProposalStatus.Failed);
        host.Writes.Should().Be(1);
    }

    [Fact]
    public async Task CancellingDuringProposalDispatch_Should_PreserveItsInFlightEvidenceAndNeverClaimTheSendWasPrevented()
    {
        using var host = new Host(); await host.InitializeAsync();
        Guid runId = await host.StartWorkflowAsync(GmailActionKeys.SendMessage); await host.ContinueAsync(runId);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Scenario.Actions.Setup(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConnectorProviderCall call, CancellationToken _) => { host.Writes++; entered.TrySetResult(); await release.Task; return Host.Receipt(call); });
        host.Scenario.Current.Id = host.Scenario.ReviewerId;
        using IServiceScope deciding = host.Scenario.Scope();
        var db = deciding.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        Task<ToolApprovalDecisionResult> decision = deciding.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using IServiceScope cancelling = host.Scenario.Scope(); IWorkflowRunService runs = cancelling.ServiceProvider.GetRequiredService<IWorkflowRunService>();
            WorkflowRunDetail current = (await runs.GetAsync(runId))!;
            WorkflowRunDetail cancelled = await runs.CancelAsync(runId, new(current.ExpectedVersion));
            cancelled.Status.Should().Be(WorkflowRunStatuses.Cancelled);
            cancelled.Steps.Single(step => step.ActionKey == GmailActionKeys.SendMessage).Status.Should().Be("NeedsAttention");
        }
        finally { release.TrySetResult(); }
        (await decision).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        (await host.ContinueAsync(runId, 1)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        (await deciding.ServiceProvider.GetRequiredService<IConnectorMutationService>().GetStatusAsync(request.Id))!.Status.Should().Be("Completed");
        host.Writes.Should().Be(1);
    }
    [Theory]
    [InlineData(GmailActionKeys.SendMessage, true)]
    [InlineData(GmailActionKeys.CreateDraft, false)]
    [InlineData(GmailActionKeys.Archive, false)]
    public async Task ActualGatedTool_Should_PersistOneApproval_ExecuteOnlyAfterConfirmation_AndReplayItsReceipt(string action, bool high)
    {
        using var host = new Host();
        await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        AIFunction tool = host.Tool(scope, action);
        AIFunctionArguments arguments = host.Arguments(action);
        arguments["connectionId"] = host.ConnectionId.ToString("D").ToUpperInvariant();

        object? pending = await tool.InvokeAsync(arguments);
        await tool.InvokeAsync(host.Arguments(action));
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        request.InvocationKey.Should().HaveLength(64);
        (await db.Proposals.CountAsync()).Should().Be(high ? 1 : 0);
        host.Writes.Should().Be(0);
        if (high) pending.Should().BeOfType<ToolApprovalQueuedResult>(); else pending.Should().BeOfType<ToolApprovalRequiredResult>();

        ToolApprovalDecisionResult approved = await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));
        approved.Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        host.Writes.Should().Be(high ? 1 : 0);
        await tool.InvokeAsync(host.Arguments(action));
        host.Writes.Should().Be(1);
        var replay = await tool.InvokeAsync(host.Arguments(action));

        replay.Should().BeOfType<ToolApprovalExecutionResult>().Which.Status.Should().Be("Completed");
        host.Writes.Should().Be(1);
        (await db.ToolApprovalRequests.CountAsync()).Should().Be(1);
        ConnectorJson.Read<ConnectorMutationJournal>((await db.ToolApprovalRequests.SingleAsync()).ExecutionJson!).Status.Should().Be("Completed");
        if (high) (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Applied);
    }

    [Fact]
    public async Task InvocationKey_Should_RejectChangedArgumentsInsteadOfCreatingAnotherApproval()
    {
        using var host = new Host(); await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        AIFunction tool = host.Tool(scope, GmailActionKeys.SendMessage);
        await tool.InvokeAsync(host.Arguments(GmailActionKeys.SendMessage));
        var changed = new AIFunctionArguments(ConnectorMutationArguments.ToolArguments(host.ConnectionId,
            ConnectorJson.Element(new GmailSendRequest("operation-1", "protected-message", new string('b', 64)))));

        await ((Func<Task>)(async () => await tool.InvokeAsync(changed))).Should().ThrowAsync<PermissionDeniedException>();

        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await db.ToolApprovalRequests.CountAsync()).Should().Be(1);
        (await db.Proposals.CountAsync()).Should().Be(1); host.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("{\"idempotencyKey\":\"one\",\"IdempotencyKey\":\"two\",\"messageReference\":\"reference\",\"contentHash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    [InlineData("{\"idempotencyKey\":\"one\",\"messageReference\":\"reference\",\"messageReference\":\"other\",\"contentHash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    public void TypedMutationArguments_Should_RejectDuplicateAndCaseAliasedProperties(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        ((Action)(() => GmailActionContracts.Normalize(new(GmailActionKeys.SendMessage, 1), document.RootElement))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("request")]
    [InlineData("proposal")]
    public async Task RevokedMailboxGrant_Should_BlockBothApprovalEntrypointsBeforeStatusOrProviderChanges(string entrypoint)
    {
        using var host = new Host(); await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        await host.Tool(scope, GmailActionKeys.SendMessage).InvokeAsync(host.Arguments(GmailActionKeys.SendMessage));
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await platform.ConnectorConnectionGrants.SingleAsync()).RevokedAt = host.Scenario.Clock.UtcNow;
        await platform.SaveChangesAsync();

        if (entrypoint == "request")
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        else
            await ((Func<Task>)(() => scope.ServiceProvider.GetRequiredService<IProposalApprovalService>().ApproveAsync(request.ProposalId!.Value))).Should().ThrowAsync<ConnectorAccessException>();

        (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed);
        request.Status.Should().Be(ToolApprovalRequestStatus.Pending); request.ExecutionJson.Should().BeNull(); host.Writes.Should().Be(0);
    }

    [Fact]
    public async Task SameTenantOutsider_Should_NotReadOrApproveAnExistingPrivateMailboxProposal()
    {
        using var host = new Host(); await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        await host.Tool(scope, GmailActionKeys.SendMessage).InvokeAsync(host.Arguments(GmailActionKeys.SendMessage));
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        host.Scenario.Current.Id = host.Scenario.OutsiderId;
        host.Scenario.Allowed.Add((host.Scenario.OutsiderId, "Integration.Use"));
        IProposalApprovalService proposals = scope.ServiceProvider.GetRequiredService<IProposalApprovalService>();

        (await proposals.GetByIdAsync(request.ProposalId!.Value)).Should().BeNull();
        await ((Func<Task>)(() => proposals.ApproveAsync(request.ProposalId.Value))).Should().ThrowAsync<PermissionDeniedException>();

        (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed); host.Writes.Should().Be(0);
    }

    [Fact]
    public async Task ChangedProviderAccount_Should_InvalidateThePendingReviewBeforeAnyDispatch()
    {
        using var host = new Host(); await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        await host.Tool(scope, GmailActionKeys.SendMessage).InvokeAsync(host.Arguments(GmailActionKeys.SendMessage));
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await platform.ConnectorConnections.SingleAsync()).ProviderAccountId = "replacement-account"; await platform.SaveChangesAsync();

        (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);

        (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Proposed); request.ExecutionJson.Should().BeNull(); host.Writes.Should().Be(0);
    }

    [Fact]
    public async Task UnknownDelivery_Should_StayTerminalOnTheProposal_AndRecoverOnlyFromVerifiedReceiptWithoutResending()
    {
        using var host = new Host(); await host.InitializeAsync();
        host.Scenario.Actions.Setup(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .Returns((ConnectorProviderCall _, CancellationToken _) => { host.Writes++; throw new ConnectorProviderException("ProviderUnavailable", true); });
        using IServiceScope scope = host.Scenario.Scope();
        AIFunction tool = host.Tool(scope, GmailActionKeys.SendMessage);
        await tool.InvokeAsync(host.Arguments(GmailActionKeys.SendMessage));
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); ToolApprovalRequest request = await db.ToolApprovalRequests.SingleAsync();

        await ((Func<Task>)(() => scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))))
            .Should().ThrowAsync<ConnectorProviderException>();
        (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Failed);
        (await tool.InvokeAsync(host.Arguments(GmailActionKeys.SendMessage))).Should().BeOfType<ToolApprovalExecutionResult>().Which.Status.Should().Be("NeedsAttention");
        IConnectorMutationService mutations = scope.ServiceProvider.GetRequiredService<IConnectorMutationService>();
        (await mutations.GetStatusAsync(request.Id, reconcile: true))!.Status.Should().Be("NeedsAttention");
        host.Scenario.Actions.Setup(provider => provider.ReconcileAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectorProviderCall call, CancellationToken _) => new(Host.Receipt(call, reconciled: true)));

        (await mutations.GetStatusAsync(request.Id, reconcile: true))!.Status.Should().Be("Completed");
        (await tool.InvokeAsync(host.Arguments(GmailActionKeys.SendMessage))).Should().BeOfType<ToolApprovalExecutionResult>().Which.Status.Should().Be("Completed");

        host.Writes.Should().Be(1);
        (await db.Proposals.SingleAsync()).Status.Should().Be(ProposalStatus.Failed, "receipt reconciliation preserves the original terminal proposal outcome");
        (await db.ToolApprovalRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task LowPreparation_Should_AuditOnlyItsHash_AndRequireTheTargetActionGrant()
    {
        using var host = new Host(); await host.InitializeAsync();
        using IServiceScope scope = host.Scenario.Scope();
        AIFunction prepare = ConnectorTools.CreateAll(scope.ServiceProvider).OfType<AIFunction>().Single(tool => tool.Name == "gmail_prepare_message");
        var arguments = new AIFunctionArguments(new Dictionary<string, object?> { ["connectionId"] = host.ConnectionId,
            ["request"] = new GmailPrepareMessageRequest(GmailActionKeys.SendMessage, ["private@example.test"], "private subject", "private body") });
        await prepare.InvokeAsync(arguments);
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest audit = await db.ToolApprovalRequests.SingleAsync();
        audit.ArgumentsRedactedJson.Should().NotContain("private@").And.NotContain("private subject").And.NotContain("private body");
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await platform.ConnectorConnectionGrants.SingleAsync()).AllowedActionsJson = ConnectorJson.Serialize(new[] { new ConnectorActionRef(GmailActionKeys.PrepareMessage, 1) });
        await platform.SaveChangesAsync();

        await ((Func<Task>)(async () => await prepare.InvokeAsync(arguments))).Should().ThrowAsync<ConnectorAccessException>();
        host.Preparations.Should().Be(1); host.Writes.Should().Be(0);
    }

    private sealed class Host : IDisposable
    {
        public ConnectorScenario Scenario { get; }
        public Guid SubjectId { get; } = Guid.NewGuid();
        public Guid ConnectionId { get; private set; }
        public int Writes { get; set; }
        public int Preparations { get; private set; }
        public Host()
        {
            string name = "GmailMutations_" + Guid.NewGuid();
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("true");
            var facts = new Mock<IWorkflowFactsProvider>(); facts.SetupGet(provider => provider.SubjectType).Returns("Invoice"); facts.SetupGet(provider => provider.Schema).Returns("InvoiceFacts@1");
            facts.Setup(provider => provider.LoadAsync(SubjectId, It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowFactsSnapshot("InvoiceFacts@1", new Dictionary<string, JsonElement> { ["invoice.id"] = ConnectorJson.Element(SubjectId) }, "version-1"));
            Scenario = new(configure: services =>
            {
                services.AddLogging(); services.AddDbContext<AgentsDbContext>(options => options.UseInMemoryDatabase(name));
                services.AddSingleton(settings.Object); services.AddSingleton(facts.Object);
                services.AddScoped<WorkflowInvocationContext>(); services.AddScoped<WorkflowExecutionAuthorization>(); services.AddScoped<WorkflowReviewerAuthorization>();
                services.AddScoped<WorkflowAuthorization>(); services.AddScoped<WorkflowApprovalAuthority>(); services.AddScoped<WorkflowContinuationWriter>();
                services.AddScoped<WorkflowActionRegistry>(); services.AddScoped<WorkflowStepExecutionService>(); services.AddScoped<IGraphWorkflowRunner, GraphWorkflowRunner>();
                services.AddScoped<WorkflowRunService>(); services.AddScoped<IWorkflowRunService>(provider => provider.GetRequiredService<WorkflowRunService>());
                services.AddScoped<IWorkflowConnectorContextReader, WorkflowConnectorContextReader>();
                services.AddScoped<IConnectorService>(provider => provider.GetRequiredService<ConnectorService>());
                services.AddScoped<IConnectorMutationService, ConnectorMutationService>(); services.AddScoped<IConnectorMutationAuthority, ConnectorMutationAuthority>();
                services.AddScoped<IToolApprovalRequestStore, ToolApprovalRequestStore>(); services.AddScoped<IAgentProposalStore, AgentProposalStore>();
                services.AddScoped<IProposalApprovalService, ProposalApprovalService>(); services.AddScoped<IToolApprovalService, ToolApprovalService>();
                services.AddScoped<IProposalDispatcher, ProposalDispatcher>(); services.AddScoped<IProposalRejectionDispatcher, ProposalRejectionDispatcher>();
                services.AddSingleton<IProposalApprovalPolicy, ProposalApprovalPolicy>(); services.AddSingleton<IToolApprovalGate, ToolApprovalGate>();
                services.AddSingleton<IToolApprovalManifest, ConnectorToolApprovalManifest>(); services.AddSingleton(Mock.Of<IToolApprovalAuditSink>());
                services.AddScoped<IAgentMutationGuard, ConnectorMutationGuard>();
                foreach (string key in new[] { GmailActionKeys.Archive, GmailActionKeys.SendMessage })
                    services.AddScoped<IWorkflowActionHandler>(provider => new ConnectorWorkflowAction(provider.GetRequiredService<IConnectorDescriptor>().Descriptor.Actions.Single(action => action.Key == key),
                        provider.GetRequiredService<IConnectorService>(), provider.GetServices<IConnectorActionProvider>(), provider.GetRequiredService<WorkflowInvocationContext>(), provider.GetRequiredService<IConnectorMutationService>()));
                foreach (string key in GmailActionPolicies.ToolNames.Keys.Where(key => GmailActionPolicies.Approval(key)?.Tier == ToolApprovalTier.High))
                    services.AddKeyedScoped<IProposalHandler>(GmailActionPolicies.Approval(key)!.ProposalType!, (provider, type) => new ConnectorMutationProposalHandler((string)type!, provider.GetRequiredService<IConnectorMutationService>()));
            });
            Scenario.Actions.Setup(provider => provider.PreviewAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ConnectorProviderCall call, CancellationToken _) => new ToolApprovalPreview("Review Gmail action", null, ConnectorJson.Element(new { call.ConnectionId, call.ProviderAccountId, reviewed = true })));
            Scenario.Actions.Setup(provider => provider.ReconcileAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConnectorMutationReconciliation(null));
            Scenario.Actions.Setup(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ConnectorProviderCall call, CancellationToken _) =>
                {
                    if (call.Action.Key == GmailActionKeys.PrepareMessage) { Preparations++; return ConnectorJson.Element(new GmailPreparedMessage("protected-message", new string('a', 64), "message@aonik.invalid", GmailActionKeys.SendMessage)); }
                    Writes++; return Receipt(call);
                });
        }
        public async Task InitializeAsync()
        {
            await Scenario.InitializeAsync();
            Scenario.Tokens = Scenario.Tokens with { GrantedScopes = [GmailActionKeys.ModifyScope, "openid", "email"] };
            using IServiceScope scope = Scenario.Scope();
            var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            ConnectorActionRef[] actions = scope.ServiceProvider.GetRequiredService<IConnectorDescriptor>().Descriptor.Actions
                .Where(action => action.Key != GmailActionKeys.PermanentDelete).Select(action => new ConnectorActionRef(action.Key, action.Version)).ToArray();
            foreach (SettingScope settingScope in new[] { SettingScope.Global, SettingScope.Tenant }) platform.Settings.Add(new Setting
            { Key = IntegrationSettingNames.GmailAllowedActions, Scope = settingScope, TenantId = settingScope == SettingScope.Tenant ? Scenario.TenantId : null, Value = ConnectorJson.Serialize(actions) });
            await platform.SaveChangesAsync();
            var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
            var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
            ConnectionRequestDetail request = await requests.CreateAsync(new("gmail", "User", actions, "Review approved mailbox operations", "mutation-account"));
            var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
            await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", Scenario.Current.OwnerId);
            ConnectionId = (await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion))).ConnectionId!.Value;
        }
        public AIFunction Tool(IServiceScope scope, string action) => ConnectorTools.CreateAll(scope.ServiceProvider).OfType<AIFunction>().Single(tool => tool.Name == GmailActionPolicies.ToolNames[action]);
        public async Task<Guid> StartWorkflowAsync(string key, bool grantReviewer = true)
        {
            using IServiceScope scope = Scenario.Scope(); var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AgentsDbContext>(); var platform = services.GetRequiredService<PlatformDbContext>();
            platform.Settings.Add(new Setting { Key = IntegrationSettingNames.GmailAllowWorkflowConnections, Scope = SettingScope.Tenant, TenantId = Scenario.TenantId, Value = "true" });
            await platform.SaveChangesAsync();
            var workflow = new Workflow { TenantId = Scenario.TenantId, Slug = "approved-gmail-operation", Name = "Approved email", State = "Draft" };
            db.Workflows.Add(workflow); await db.SaveChangesAsync();
            var grants = services.GetRequiredService<ConnectorGrantService>();
            var connection = await platform.ConnectorConnections.SingleAsync();
            ConnectorGrantDetail binding = await grants.IssueAsync(ConnectionId, new("Workflow", workflow.Id, [new(key, 1)], Convert.ToBase64String(connection.RowVersion)));
            foreach (string permission in new[] { "Workflow.Read", "Workflow.Run", "Integration.Use" }) Scenario.Allowed.Add((Scenario.ReviewerId, permission));
            if (grantReviewer) await grants.IssueAsync(ConnectionId, new("User", Scenario.ReviewerId, [new(key, 1)], Convert.ToBase64String((await platform.ConnectorConnections.SingleAsync()).RowVersion)));
            JsonElement arguments = ConnectorJson.Element(Arguments(key)["request"]);
            var parameters = new WorkflowToolParameters(key, 1, arguments.EnumerateObject().ToDictionary(property => property.Name,
                property => new WorkflowInputBinding(Literal: property.Value)), new(Scenario.ReviewerRoleId, true),
                new(ConnectionId, binding.Id, [new(key, 1)]));
            var definition = new WorkflowDefinition(1, "Workflow", workflow.Slug, workflow.Name, "",
                [new("start", "trigger", "Start", ConnectorJson.Element(new { })), new("mail", "tool", "Approved email", ConnectorJson.Element(parameters)), new("done", "end", "Done", ConnectorJson.Element(new { }))],
                [new("start-mail", "start", "mail", 0, null), new("mail-done", "mail", "done", 0, null)]);
            string json = WorkflowDefinitionCodec.Serialize(definition);
            var version = new WorkflowVersion { TenantId = Scenario.TenantId, WorkflowId = workflow.Id, DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json),
                SchemaVersion = 1, Tag = "v1", PublishedAt = Scenario.Clock.UtcNow, PublishedByUserId = Scenario.Current.OwnerId };
            db.WorkflowVersions.Add(version); workflow.State = "Active"; workflow.PublishedVersionId = version.Id; await db.SaveChangesAsync();
            return (await services.GetRequiredService<IWorkflowRunService>().StartAsync(workflow.Slug, new(version.Id, "Invoice", SubjectId, "workflow-mail-1"))).Id;
        }
        public async Task<GraphWorkflowContinuation> ContinueAsync(Guid id, int continuation = 0)
        { using IServiceScope scope = Scenario.Scope(); return await scope.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(id, continuation); }
        public AIFunctionArguments Arguments(string action) => new(ConnectorMutationArguments.ToolArguments(ConnectionId, action == GmailActionKeys.Archive
            ? ConnectorJson.Element(new GmailMessageTargets("operation-1", ["message-1"])) : ConnectorJson.Element(new GmailSendRequest("operation-1", "protected-message", new string('a', 64)))));
        public static JsonElement Receipt(ConnectorProviderCall call, bool reconciled = false) => ConnectorJson.Element(new GmailMutationReceipt(call.Action.Key, call.OperationId, "Completed", ["message-1"], Reconciled: reconciled));
        public void Dispose() => Scenario.Dispose();
    }
}
