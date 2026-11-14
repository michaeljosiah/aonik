using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Finance.Agents;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Agents.Approval;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowExecutionTests
{
    [Fact]
    public async Task AutomationDisabledAfterHumanApproval_Should_StopBeforeTheFinancialWrite()
    {
        using var host = new Host();
        Guid runId = await host.StartAutomation();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using (IServiceScope scope = host.Scope())
        {
            var agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await agents.WorkflowRuns.SingleAsync(run => run.Id == runId);
            ToolApprovalRequest request = await agents.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId);
            host.Actor.Id = host.Reviewer;
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            PlatformDbContext platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await platform.AutomationIdentities.SingleAsync()).Status = "Disabled"; await platform.SaveChangesAsync();
        }
        host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task AutomationInvoiceRun_Should_WaitForANamedHuman_AndNeverImpersonateItsConfigurer()
    {
        using var host = new Host();
        Guid runId = await host.StartAutomation();
        host.Inactive.Add(host.Actor.Author);
        host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        Guid approvalId;
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await agents.WorkflowRuns.SingleAsync(run => run.Id == runId);
            run.ActorType.Should().Be("AutomationIdentity");
            run.InitiatingUserId.Should().BeNull();
            run.ConfiguredByUserId.Should().Be(host.Actor.Author);
            ToolApprovalRequest approval = await agents.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId);
            approvalId = approval.Id;
            approval.RequestingUserId.Should().BeNull();
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        }
        host.Actor.Id = host.Reviewer; host.Actor.Authenticated = true;
        using (IServiceScope scope = host.Scope())
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).IssueApprovedByUserId.Should().Be(host.Reviewer);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("action-revoked")]
    [InlineData("scope-revoked")]
    public async Task QueuedAutomation_Should_RecheckIndependentGrants_BeforeAnEffect(string change)
    {
        using var host = new Host();
        Guid runId = await host.StartAutomation();
        using (IServiceScope scope = host.Scope())
        {
            PlatformDbContext platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            AutomationIdentity identity = await platform.AutomationIdentities.SingleAsync();
            var grants = JsonSerializer.Deserialize<List<AutomationWorkflowGrant>>(identity.GrantsJson, WorkflowDefinitionCodec.JsonOptions)!;
            if (change == "disabled") identity.Status = "Disabled";
            if (change == "expired") identity.ExpiresAt = host.Clock.UtcNow;
            if (change == "action-revoked") identity.GrantsJson = JsonSerializer.Serialize(grants.Select(grant => grant with { Actions = [] }), WorkflowDefinitionCodec.JsonOptions);
            if (change == "scope-revoked") identity.GrantsJson = JsonSerializer.Serialize(grants.Select(grant => grant with { SubjectIds = [Guid.NewGuid()] }), WorkflowDefinitionCodec.JsonOptions);
            await platform.SaveChangesAsync();
        }
        host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherRun_Should_NotConsumeAnApprovedStep_WithMatchingArguments(bool automated)
    {
        using var host = new Host();
        Guid firstId = automated ? await host.StartAutomation() : await host.Start();
        (await host.Continue(firstId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        Guid approvalId;
        Guid secondId;
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun first = await agents.WorkflowRuns.SingleAsync(run => run.Id == firstId);
            approvalId = (await agents.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == first.PendingStepRunId)).Id;
            host.Actor.Id = host.Reviewer;
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            host.Actor.Id = host.Actor.Author;
            WorkflowRunService admission = scope.ServiceProvider.GetRequiredService<WorkflowRunService>();
            secondId = automated
                ? await admission.AdmitAsync(first.ActorId!.Value, first.WorkflowVersionId!.Value, "Invoice", first.SubjectId!.Value, "second-automatic")
                : (await admission.StartAsync("invoice-route", new(first.WorkflowVersionId!.Value, "Invoice", first.SubjectId!.Value, "second-manual"))).Id;
        }
        (await host.Continue(secondId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope final = host.Scope();
        ToolApprovalRequest original = await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.Id == approvalId);
        original.ConsumedAt.Should().BeNull();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task AonikStartTool_Should_ConfirmThePublishedVersionAndFacts_BeforeAdmittingAnotherRun()
    {
        using var host = new Host();
        await host.Start();
        using IServiceScope scope = host.Scope();
        IServiceProvider services = scope.ServiceProvider;
        AgentsDbContext database = services.GetRequiredService<AgentsDbContext>();
        WorkflowRun original = await database.WorkflowRuns.SingleAsync();
        AIFunction start = WorkflowTools.CreateAll(services).OfType<AIFunction>().Single(tool => tool.Name == "aonik_start_workflow");
        var request = new StartWorkflowRunRequest(original.WorkflowVersionId!.Value, "Invoice", original.SubjectId!.Value, "agent-start-1");
        async Task<object?> Invoke() => await start.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["slug"] = "invoice-route", ["request"] = request }));
        ToolApprovalRequiredResult first = As<ToolApprovalRequiredResult>(await Invoke());
        first.Preview!.After!.Value.GetProperty("facts").GetProperty("invoice.total").GetString().Should().Be("1250.25");
        await services.GetRequiredService<IToolApprovalService>().DecideAsync(first.ApprovalRequestId!.Value, new(ToolApprovalDecisionType.Approve));
        FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();
        (await finance.Invoices.SingleAsync()).Total += 1;
        await finance.SaveChangesAsync();

        ToolApprovalRequiredResult revised = As<ToolApprovalRequiredResult>(await Invoke());

        revised.ApprovalRequestId.Should().NotBe(first.ApprovalRequestId!.Value);
        (await database.ToolApprovalRequests.SingleAsync(item => item.Id == first.ApprovalRequestId)).ConsumedAt.Should().BeNull();
        (await database.WorkflowRuns.CountAsync()).Should().Be(1);
        await services.GetRequiredService<IToolApprovalService>().DecideAsync(revised.ApprovalRequestId!.Value, new(ToolApprovalDecisionType.Approve));
        WorkflowRunDetail admitted = As<WorkflowRunDetail>(await Invoke());
        admitted.Id.Should().NotBe(original.Id);
        (await database.WorkflowRuns.CountAsync()).Should().Be(2);
    }

    private static T As<T>(object? result) => result is T typed ? typed
        : JsonSerializer.SerializeToElement(result, WorkflowDefinitionCodec.JsonOptions).Deserialize<T>(WorkflowDefinitionCodec.JsonOptions)!;

    [Fact]
    public async Task Cancellation_Should_WithdrawAnUndispatchedApproval_WithoutInventingAHumanDecision()
    {
        using var host = new Host();
        Guid runId = await host.Start();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope scope = host.Scope();
        IWorkflowRunService runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
        WorkflowRunDetail waiting = (await runs.GetAsync(runId))!;

        await runs.CancelAsync(runId, new(waiting.ExpectedVersion));

        ToolApprovalRequest approval = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests
            .SingleAsync(request => request.WorkflowStepRunId != null);
        approval.Status.Should().Be(ToolApprovalRequestStatus.Cancelled);
        approval.DecidedByUserId.Should().BeNull();
        approval.ConsumedAt.Should().BeNull();
        (await host.Continue(runId, 1)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Theory]
    [InlineData("unknown-action")]
    [InlineData("unknown-role")]
    [InlineData("wrong-fact-type")]
    [InlineData("partial-output")]
    public async Task Publication_Should_RefuseUnavailableActionsReviewersAndInvalidBindings(string invalid)
    {
        using var host = new Host();
        await host.Start();
        using IServiceScope scope = host.Scope();
        WorkflowGraphResponse graph = (await scope.ServiceProvider.GetRequiredService<IWorkflowService>().GetBySlugAsync("invoice-route"))!;
        WorkflowDefinition definition = WorkflowDefinitionCodec.FromGraph(graph);
        WorkflowToolParameters original = definition.Nodes.Single(node => node.Key == "issue").Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
        WorkflowToolParameters changed = invalid switch
        {
            "unknown-action" => original with { ContractVersion = 999 },
            "unknown-role" => original with { Reviewer = new(Guid.NewGuid()) },
            "wrong-fact-type" => original with { Inputs = new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Fact: "invoice.total") } },
            _ => original with { Inputs = new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Output: "invoiceId") } },
        };
        definition = definition with { Nodes = definition.Nodes.Select(node => node.Key == "issue"
            ? node with { Parameters = JsonSerializer.SerializeToElement(changed, WorkflowDefinitionCodec.JsonOptions) } : node).ToList() };

        IReadOnlyList<WorkflowDiagnostic> diagnostics = await scope.ServiceProvider.GetRequiredService<WorkflowPublicationValidator>().ValidateAsync(definition);

        diagnostics.Should().Contain(diagnostic => diagnostic.Code == "ActionContract");
    }

    [Fact]
    public async Task InvoiceRouting_Should_CheckpointForAHuman_AndIssueOnceAfterAFreshWorkerResumes()
    {
        using var host = new Host();
        Guid runId = await host.Start();

        GraphWorkflowContinuation waiting = await host.Continue(runId);

        waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, waiting.ErrorCode);
        Guid approvalId;
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await db.WorkflowRuns.SingleAsync(run => run.Id == runId);
            run.CheckpointId.Should().NotBeNullOrEmpty();
            run.FrameworkSessionId.Should().NotBeNullOrEmpty();
            run.LeaseId.Should().BeNull();
            (await db.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "route")).SelectedRoute.Should().Be("standard");
            approvalId = (await db.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null)).Id;
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        }

        host.Actor.Id = host.Reviewer;
        using (IServiceScope scope = host.Scope())
        {
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().Set<OutboxMessage>().CountAsync()).Should().BeGreaterThan(1);
        }

        GraphWorkflowContinuation completed = await host.Continue(runId);
        completed.Status.Should().Be(WorkflowRunStatuses.Completed, completed.ErrorCode);
        completed.Outcome.Should().Be("Issued");
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowStepRun issue = await db.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "issue");
            issue.Status.Should().Be("Completed");
            issue.Attempts.Should().Be(1);
            Invoice invoice = await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync();
            invoice.Status.Should().Be("Issued");
            invoice.IssueOperationId.Should().Be(issue.OperationId);
            invoice.IssueApprovalRequestId.Should().Be(approvalId);
            invoice.IssueApprovedByUserId.Should().Be(host.Reviewer);
            (await db.ToolApprovalRequests.SingleAsync(request => request.Id == approvalId)).ConsumedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task InvoiceRouting_Should_RefuseSelfApproval_AndStopAfterAnAuthorizedRejection()
    {
        using var host = new Host();
        Guid runId = await host.Start();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope scope = host.Scope();
        Guid approvalId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests
            .SingleAsync(request => request.WorkflowStepRunId != null)).Id;
        IToolApprovalService approvals = scope.ServiceProvider.GetRequiredService<IToolApprovalService>();

        (await approvals.DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
        host.Actor.Id = host.Reviewer;
        (await approvals.DecideAsync(approvalId, new(ToolApprovalDecisionType.Reject))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Rejected);
        GraphWorkflowContinuation stopped = await host.Continue(runId);

        stopped.Status.Should().Be(WorkflowRunStatuses.Completed, stopped.ErrorCode);
        stopped.Outcome.Should().Be("ApprovalRejected");
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task InvoiceRouting_Should_RefuseApproval_WhenTheStoredInvoiceChanges()
    {
        using var host = new Host();
        Guid runId = await host.Start();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope scope = host.Scope();
        FinanceDbContext finance = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        Invoice invoice = await finance.Invoices.SingleAsync();
        invoice.Total += 1;
        await finance.SaveChangesAsync();
        Guid approvalId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests
            .SingleAsync(request => request.WorkflowStepRunId != null)).Id;
        host.Actor.Id = host.Reviewer;

        Func<Task> approve = async () => { await scope.ServiceProvider.GetRequiredService<IToolApprovalService>()
            .DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)); };

        await approve.Should().ThrowAsync<WorkflowConflictException>();
        invoice.Status.Should().Be("Draft");
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.Id == approvalId))
            .Status.Should().Be(ToolApprovalRequestStatus.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvoiceRouting_Should_ReconcileAnInvoiceWriteCommittedBeforeTheWorkerStopped(bool cancel)
    {
        using var host = new Host();
        Guid runId = await host.Start();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        host.Actor.Id = host.Reviewer;
        using (IServiceScope scope = host.Scope())
        {
            Guid approvalId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests
                .SingleAsync(request => request.WorkflowStepRunId != null)).Id;
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve));
        }
        host.CrashAfterIssue = true;

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);

        using (IServiceScope scope = host.Scope())
        {
            IWorkflowRunService runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
            WorkflowRunDetail held = (await runs.GetAsync(runId))!;
            held.Steps.Single(step => step.NodeKey == "issue").Status.Should().Be("Executing");
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
            if (cancel) await runs.CancelAsync(runId, new(held.ExpectedVersion));
            else await runs.ResumeAsync(runId, new(held.ExpectedVersion));
        }
        GraphWorkflowContinuation recovered = await host.Continue(runId, 1);
        recovered.Status.Should().Be(cancel ? WorkflowRunStatuses.Cancelled : WorkflowRunStatuses.Completed, recovered.ErrorCode);
        using (IServiceScope scope = host.Scope())
        {
            WorkflowStepRun issue = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.SingleAsync(step => step.NodeKey == "issue");
            issue.Status.Should().Be("Completed");
            issue.Attempts.Should().Be(1, "a reconciled domain receipt must never dispatch another write");
            issue.ResultJson.Should().Contain(issue.OperationId.ToString());
        }
    }

    [Theory]
    [InlineData("reviewer")]
    [InlineData("initiator")]
    [InlineData("retired")]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("facts")]
    public async Task InvoiceRouting_Should_RecheckCurrentAuthorityAndFacts_AfterHumanApproval(string change)
    {
        using var host = new Host();
        Guid runId = await host.Start();
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        host.Actor.Id = host.Reviewer;
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            Guid approvalId = (await db.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null)).Id;
            WorkflowRunDetail review = (await scope.ServiceProvider.GetRequiredService<IWorkflowRunService>().GetAsync(runId))!;
            review.Steps.Single(step => step.NodeKey == "issue").Approval!.CanDecide.Should().BeTrue();
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve));
            if (change == "reviewer") host.Denied.Add((host.Reviewer, "Invoice.Issue"));
            if (change == "initiator") host.Inactive.Add(host.Actor.Author);
            if (change == "retired") { (await db.Workflows.SingleAsync()).RetiredAt = host.Clock.UtcNow; await db.SaveChangesAsync(); }
            if (change == "disabled") host.Enabled = false;
            if (change == "expired") host.Clock.UtcNow = host.Clock.UtcNow.AddHours(1);
            if (change == "facts")
            {
                FinanceDbContext finance = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                (await finance.Invoices.SingleAsync()).Total += 1;
                await finance.SaveChangesAsync();
            }
        }

        GraphWorkflowContinuation stopped = await host.Continue(runId);

        stopped.Status.Should().Be(change == "retired" ? WorkflowRunStatuses.Cancelled
            : change == "expired" ? WorkflowRunStatuses.Completed : WorkflowRunStatuses.NeedsAttention);
        using IServiceScope verify = host.Scope();
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task InvoiceAdapter_Should_RefuseACallerSuppliedApprovalId_OutsideThePersistedWorkerInvocation()
    {
        using var host = new Host();
        Guid runId = await host.Start();
        using IServiceScope scope = host.Scope();
        WorkflowRun run = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(item => item.Id == runId);
        WorkflowFactsSnapshot facts = (await scope.ServiceProvider.GetRequiredService<InvoiceWorkflowFactsProvider>()
            .LoadAsync(run.SubjectId!.Value, new("User", host.Actor.Author)))!;
        var invented = new WorkflowActionInvocation(run.Id, Guid.NewGuid(), Guid.NewGuid(), new("User", host.Actor.Author, host.Actor.Author),
            "Invoice", run.SubjectId.Value, facts.SubjectVersion, WorkflowFactsHash.Compute(facts),
            JsonSerializer.SerializeToElement(new { invoiceId = run.SubjectId.Value }), Guid.NewGuid());

        Func<Task> bypass = async () => { await scope.ServiceProvider.GetRequiredService<InvoiceIssueWorkflowAction>().ExecuteAsync(invented); };

        await bypass.Should().ThrowAsync<PermissionDeniedException>();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    private sealed class Host : IDisposable
    {
        private readonly ServiceProvider _provider;
        public Actor Actor { get; } = new();
        public Guid Reviewer { get; } = Guid.NewGuid();
        private readonly Guid _roleId = Guid.NewGuid();
        private readonly Guid _invoiceId = Guid.NewGuid();
        public bool CrashAfterIssue { get; set; }
        public bool Enabled { get; set; } = true;
        public TestClock Clock { get; } = new();
        public HashSet<Guid> Inactive { get; } = [];
        public HashSet<(Guid, string)> Denied { get; } = [];

        public Host()
        {
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(service => service.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid userId, string permission, CancellationToken _) => !Denied.Contains((userId, permission)));
            var users = new Mock<IUserDirectoryReader>();
            users.Setup(service => service.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<UserDirectoryItem>)ids.Select(id => new UserDirectoryItem(id, null, Inactive.Contains(id) ? "Inactive" : "Active")).ToList());
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Enabled ? "true" : "false");
            var roles = new Mock<ITenantRoleReader>();
            roles.Setup(reader => reader.ExistsAsync(It.IsAny<Guid>(), _roleId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            roles.Setup(reader => reader.IsMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), _roleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid userId, Guid _, CancellationToken _) => userId == Reviewer || userId == Actor.Author);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantProvider>(new Tenant());
            services.AddSingleton<ICurrentUserProvider>(Actor);
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton(permissions.Object);
            services.AddSingleton(users.Object);
            services.AddSingleton(roles.Object);
            services.AddSingleton(settings.Object);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            string database = $"WorkflowExecution_{Guid.NewGuid()}";
            services.AddDbContext<AgentsDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddDbContext<FinanceDbContext>(options => options.UseInMemoryDatabase(database + "_finance"));
            services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(database + "_platform"));
            services.AddScoped<IAutomationIdentityAuthorization, AutomationIdentityAuthorization>();
            services.AddScoped<IWorkflowService, WorkflowService>();
            services.AddScoped<WorkflowRunService>();
            services.AddScoped<IWorkflowRunService>(provider => provider.GetRequiredService<WorkflowRunService>());
            services.AddScoped<WorkflowAuthorization>();
            services.AddScoped<WorkflowDraftPreparation>();
            services.AddScoped<WorkflowGovernancePreparation>();
            services.AddScoped<WorkflowApprovalAuthority>();
            services.AddScoped<WorkflowGovernanceService>();
            services.AddScoped<IWorkflowGovernanceService>(provider => provider.GetRequiredService<WorkflowGovernanceService>());
            services.AddScoped<IAgentMutationGuard, WorkflowMutationGuard>();
            services.AddScoped<WorkflowPublicationValidator>();
            services.AddScoped<WorkflowActionRegistry>();
            services.AddScoped<WorkflowInvocationContext>();
            services.AddScoped<WorkflowExecutionAuthorization>();
            services.AddScoped<WorkflowReviewerAuthorization>();
            services.AddScoped<WorkflowContinuationWriter>();
            services.AddScoped<WorkflowStepExecutionService>();
            services.AddScoped<IWorkflowApprovalReader, WorkflowApprovalReader>();
            services.AddScoped<IGraphWorkflowRunner, GraphWorkflowRunner>();
            services.AddScoped<IToolApprovalRequestStore, ToolApprovalRequestStore>();
            services.AddScoped<IAgentProposalStore, AgentProposalStore>();
            services.AddScoped<IToolApprovalService, ToolApprovalService>();
            services.AddScoped<IProposalApprovalService, ProposalApprovalService>();
            services.AddScoped<IProposalApprovalPolicy, ProposalApprovalPolicy>();
            services.AddScoped<IProposalDispatcher, ProposalDispatcher>();
            services.AddSingleton(Mock.Of<IProposalRejectionDispatcher>());
            services.AddSingleton<IToolApprovalManifest, WorkflowToolApprovalManifest>();
            services.AddSingleton<IToolApprovalManifest, FinanceToolApprovalManifest>();
            services.AddSingleton<IToolApprovalAuditSink, LoggingToolApprovalAuditSink>();
            services.AddSingleton<IToolApprovalGate, ToolApprovalGate>();
            foreach (string type in new[] { WorkflowToolApprovalManifest.PublishType, WorkflowToolApprovalManifest.RetireType })
                services.AddKeyedScoped<IProposalHandler>(type, (provider, key) => new WorkflowGovernanceProposalHandler((string)key!, provider.GetRequiredService<WorkflowGovernanceService>()));
            services.AddScoped<InvoiceWorkflowFactsProvider>();
            services.AddScoped<IWorkflowFactsProvider>(provider => provider.GetRequiredService<InvoiceWorkflowFactsProvider>());
            services.AddScoped<InvoiceIssueWorkflowAction>();
            services.AddScoped<IWorkflowActionHandler>(provider => new CrashAfterCommitAction(this, provider.GetRequiredService<InvoiceIssueWorkflowAction>()));
            services.AddScoped<BillingService>();
            services.AddScoped<LedgerPostingService>();
            services.AddSingleton<FinanceMetrics>();
            _provider = services.BuildServiceProvider();
        }

        public IServiceScope Scope() => _provider.CreateScope();
        public async Task<Guid> StartAutomation()
        {
            Guid manualId = await Start();
            using IServiceScope scope = Scope();
            var services = scope.ServiceProvider;
            WorkflowRun manual = await services.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.Id == manualId);
            var identity = new AutomationIdentity { TenantId = manual.TenantId, Name = "Invoice automation", AccountableUserId = Actor.Author, ConfiguredByUserId = Actor.Author,
                PermissionsJson = JsonSerializer.Serialize(new[] { "Workflow.Read", "Workflow.Run", "Invoice.Read", "Invoice.Issue" }),
                GrantsJson = JsonSerializer.Serialize(new[] { new AutomationWorkflowGrant(manual.WorkflowVersionId!.Value, "Invoice", false, [_invoiceId], [new("finance.invoices.issue", 1)]) }, WorkflowDefinitionCodec.JsonOptions) };
            PlatformDbContext platform = services.GetRequiredService<PlatformDbContext>();
            platform.AutomationIdentities.Add(identity); await platform.SaveChangesAsync();
            return await services.GetRequiredService<WorkflowRunService>().AdmitAsync(identity.Id, manual.WorkflowVersionId!.Value, "Invoice", _invoiceId, "automatic-invoice-1");
        }
        public async Task<GraphWorkflowContinuation> Continue(Guid runId, int continuation = 0)
        {
            using IServiceScope scope = Scope();
            return await scope.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(runId, continuation);
        }
        public async Task<Guid> Start()
        {
            using IServiceScope scope = Scope();
            IServiceProvider services = scope.ServiceProvider;
            FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();
            finance.Invoices.Add(new() { Id = _invoiceId, TenantId = services.GetRequiredService<ITenantProvider>().GetCurrentTenantId(),
                CustomerAccountId = Guid.NewGuid(), Currency = "GBP", Total = 1250.25m, Status = "Draft", CreatedBy = Actor.Author, ProvenanceJson = "{}" });
            await finance.SaveChangesAsync();
            IWorkflowService workflows = services.GetRequiredService<IWorkflowService>();
            WorkflowGraphResponse draft = await workflows.CreateAsync(Draft());
            WorkflowValidation validation = (await workflows.ValidateAsync(draft.Slug))!;
            validation.Diagnostics.Where(item => item.Severity == "Error").Should().BeEmpty();
            WorkflowGovernanceRequestResult publication = await services.GetRequiredService<WorkflowGovernanceService>()
                .RequestAsync(new(draft.Slug, draft.ExpectedVersion!, validation.DefinitionHash, null), false);
            (await services.GetRequiredService<IToolApprovalService>().DecideAsync(publication.ApprovalRequestId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            Guid versionId = (await services.GetRequiredService<AgentsDbContext>().Workflows.SingleAsync()).PublishedVersionId!.Value;
            return (await services.GetRequiredService<IWorkflowRunService>().StartAsync(draft.Slug, new(versionId, "Invoice", _invoiceId, "invoice-route-1"))).Id;
        }
        private WorkflowSaveRequest Draft()
        {
            DecisionTable table = DecisionTableEvaluatorTests.InvoiceTable() with
            {
                Fixtures = [new("normal", "Normal", DecisionTableEvaluatorTests.Facts("100.00"), "Completed", ["standard"]),
                    new("boundary", "Boundary", DecisionTableEvaluatorTests.Facts("5000.00"), "Completed", ["elevated"]),
                    new("missing", "MissingData", new Dictionary<string, JsonElement>(), "NeedsManualReview", [])],
            };
            string decision = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions);
            string action = JsonSerializer.Serialize(new WorkflowToolParameters(InvoiceIssueWorkflowAction.ActionKey, 1,
                new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Fact: "invoice.id") }, new(_roleId)), WorkflowDefinitionCodec.JsonOptions);
            return new("invoice-route", "Invoice approval", "Invoice routing", "Draft", "v0.1", false, "", null, [],
                [new("start", "trigger", "Start", "", "", 0, 0, "{}"), new("route", "decision", "Approval routing", "", "", 1, 0, decision),
                 new("issue", "tool", "Issue invoice", "", "", 2, 0, action), new("done", "end", "Done", "", "", 3, 0, "{}"),
                 new("manual", "end", "Manual review", "", "", 3, 1, "{\"outcome\":\"NeedsManualReview\"}")],
                [new("start", "route", 0, ""), new("route", "issue", 0, "", RouteKey: "standard"), new("route", "manual", 1, "", RouteKey: "elevated"),
                 new("route", "manual", 2, "", RouteKey: "manual"), new("issue", "done", 0, "")], null);
        }
        public void Dispose() => _provider.Dispose();
    }
    private sealed class CrashAfterCommitAction(Host host, InvoiceIssueWorkflowAction inner) : IWorkflowActionHandler
    {
        public WorkflowActionDescriptor Descriptor => inner.Descriptor;
        public void ValidateArguments(JsonElement arguments) => inner.ValidateArguments(arguments);
        public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        {
            WorkflowActionResult result = await inner.ExecuteAsync(invocation, cancellationToken);
            if (host.CrashAfterIssue) { host.CrashAfterIssue = false; throw new InvalidOperationException("Simulated worker loss after the domain commit."); }
            return result;
        }
        public Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
            => inner.ReconcileAsync(invocation, cancellationToken);
    }
    private sealed class Actor : ICurrentUserProvider
    {
        public bool Authenticated { get; set; } = true;
        public Guid Author { get; } = Guid.NewGuid();
        private Guid? _id;
        public Guid Id { get => _id ?? Author; set => _id = value; }
        public Guid? GetCurrentUserId() => Authenticated ? Id : null;
        public bool TryGetCurrentUserId(out Guid id) { id = Authenticated ? Id : Guid.Empty; return Authenticated; }
    }
    private sealed class Tenant : ITenantProvider
    {
        private readonly Guid _id = Guid.NewGuid();
        public Guid GetCurrentTenantId() => _id;
        public bool TryGetCurrentTenantId(out Guid id) { id = _id; return true; }
    }
    private sealed class TestClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
}
