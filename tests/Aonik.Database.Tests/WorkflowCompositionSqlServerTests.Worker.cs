using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Database.Tests;

public sealed partial class WorkflowCompositionSqlServerTests
{
    [SkippableFact]
    public async Task InvoiceWorkItem_Should_RecoverACommittedIssue_AfterWorkerLossAndWorkflowReplacement()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var scenario = new Scenario();
        Guid roleId = Guid.NewGuid();
        WorkflowVersion version;
        Guid runId;
        Guid approvalId;
        Guid startWakeId;
        Guid issueWakeId;
        using (ServiceProvider first = Provider(scenario, configure: ConfigureWorker))
        using (IServiceScope scope = first.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();
            finance.Invoices.Add(new Invoice { Id = scenario.InvoiceId, TenantId = scenario.TenantId, CustomerAccountId = Guid.NewGuid(),
                Currency = "GBP", Total = 42.25m, Status = "Draft", ProvenanceJson = "{}" });
            await finance.SaveChangesAsync();
            version = await SeedInvoiceWorkflowAsync(services.GetRequiredService<AgentsDbContext>());
            runId = (await services.GetRequiredService<WorkflowRunService>().StartAsync("sql-issue", new(version.Id, "Invoice",
                scenario.InvoiceId, "sql-issue-start"))).Id;
            startWakeId = (await DeliverDueWakesAsync(services, runId)).Single().EventId;
            (await first.GetRequiredService<IWorkItemDispatcher>().DispatchDueAsync(new())).Succeeded.Should().Be(1);
        }

        using (ServiceProvider reviewer = Provider(scenario, scenario.ReviewerId, ConfigureWorker))
        using (IServiceScope scope = reviewer.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            var saved = services.GetRequiredService<AgentsDbContext>();
            var waiting = await saved.WorkflowRuns.AsNoTracking().SingleAsync(run => run.Id == runId);
            waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, waiting.ErrorCode);
            waiting.CheckpointId.Should().NotBeNullOrWhiteSpace();
            ToolApprovalRequest approval = await saved.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == waiting.PendingStepRunId);
            approvalId = approval.Id;
            (await services.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            issueWakeId = (await DeliverDueWakesAsync(services, runId)).Single(wake => wake.EventId != startWakeId).EventId;
        }

        var lostWorker = new StopAfterInvoiceCommit();
        using (ServiceProvider worker = Provider(scenario, configure: services =>
        {
            ConfigureWorker(services);
            services.AddSingleton(new DbContextOptionsBuilder<AgentsDbContext>(database.CreateOptions<AgentsDbContext>())
                .AddInterceptors(lostWorker).Options);
            services.AddSingleton(new DbContextOptionsBuilder<FinanceDbContext>(database.CreateOptions<FinanceDbContext>())
                .AddInterceptors(lostWorker).Options);
        }))
        {
            Func<Task> dispatch = async () => { await worker.GetRequiredService<IWorkItemDispatcher>().DispatchDueAsync(new()); };
            await dispatch.Should().ThrowAsync<OperationCanceledException>().WithMessage(StopAfterInvoiceCommit.Message);
        }

        Guid operationId;
        Guid occurrenceId;
        using (ServiceProvider inspection = Provider(scenario, configure: ConfigureWorker))
        using (IServiceScope scope = inspection.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            var saved = services.GetRequiredService<AgentsDbContext>();
            var workflowRun = await saved.WorkflowRuns.AsNoTracking().SingleAsync(run => run.Id == runId);
            workflowRun.Status.Should().Be(WorkflowRunStatuses.Running);
            workflowRun.LeaseExpiresAt.Should().BeAfter(scenario.Clock.UtcNow);
            WorkflowStepRun issue = await saved.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.Id == workflowRun.PendingStepRunId);
            issue.Status.Should().Be("Executing");
            issue.Attempts.Should().Be(1);
            operationId = issue.OperationId;
            var invoice = await services.GetRequiredService<FinanceDbContext>().Invoices.AsNoTracking().SingleAsync();
            invoice.Status.Should().Be("Issued");
            invoice.IssueOperationId.Should().Be(operationId);
            invoice.IssueApprovalRequestId.Should().Be(approvalId);
            invoice.IssueApprovedByUserId.Should().Be(scenario.ReviewerId);
            (await saved.ToolApprovalRequests.AsNoTracking().SingleAsync(request => request.Id == approvalId)).ConsumedAt.Should().NotBeNull();
            var occurrence = await services.GetRequiredService<PlatformDbContext>().WorkItemRuns.AsNoTracking().SingleAsync(run => run.WorkItemId == issueWakeId);
            occurrence.Outcome.Should().BeEmpty();
            occurrenceId = occurrence.Id;

            Workflow definition = await saved.Workflows.SingleAsync(workflow => workflow.Id == version.WorkflowId);
            var replacementDefinition = WorkflowRunService.ReadDefinition(version);
            replacementDefinition = replacementDefinition with { Nodes = replacementDefinition.Nodes.Select(node => node.Key == "done"
                ? node with { Parameters = JsonSerializer.SerializeToElement(new { outcome = "NeedsManualReview" }) } : node).ToArray() };
            string json = WorkflowDefinitionCodec.Serialize(replacementDefinition);
            var replacement = new WorkflowVersion { TenantId = scenario.TenantId, WorkflowId = definition.Id, Tag = "v2", SchemaVersion = 2,
                DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json), PublishedAt = scenario.Clock.UtcNow, PublishedByUserId = scenario.AuthorId };
            definition.PublishedVersionId = replacement.Id;
            saved.WorkflowVersions.Add(replacement);
            await saved.SaveChangesAsync();
        }

        scenario.Clock.UtcNow = scenario.Clock.UtcNow.AddMinutes(6);
        using ServiceProvider restarted = Provider(scenario, configure: ConfigureWorker);
        using IServiceScope finalScope = restarted.CreateScope();
        IServiceProvider final = finalScope.ServiceProvider;
        await DeliverDueWakesAsync(final, runId);
        var dispatcher = restarted.GetRequiredService<IWorkItemDispatcher>();
        (await dispatcher.DispatchDueAsync(new())).Succeeded.Should().BeGreaterThanOrEqualTo(1);
        var recovered = final.GetRequiredService<AgentsDbContext>();
        WorkflowRun result = await recovered.WorkflowRuns.AsNoTracking().SingleAsync(run => run.Id == runId);
        result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode);
        result.Outcome.Should().Be("Completed");
        result.WorkflowVersionId.Should().Be(version.Id);
        result.DefinitionHash.Should().Be(version.DefinitionHash);
        var recoveredStep = await recovered.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.RunId == runId && step.NodeKey == "issue");
        recoveredStep.Status.Should().Be("Completed");
        recoveredStep.OperationId.Should().Be(operationId);
        recoveredStep.Attempts.Should().Be(1);
        recoveredStep.ResultJson.Should().Contain(operationId.ToString());
        (await recovered.ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId == recoveredStep.Id)).Should().Be(1);
        var recoveredOccurrence = await final.GetRequiredService<PlatformDbContext>().WorkItemRuns.AsNoTracking().SingleAsync(run => run.WorkItemId == issueWakeId);
        recoveredOccurrence.Id.Should().Be(occurrenceId);
        recoveredOccurrence.Outcome.Should().Be(nameof(TaskActionOutcome.Succeeded));
        (await recovered.Set<OutboxMessage>().CountAsync(message => message.TenantId == scenario.TenantId && message.EventId == operationId))
            .Should().Be(1);

        void ConfigureWorker(IServiceCollection services)
        {
            services.AddScoped<ITenantContext>(_ => new TenantContext { TenantId = scenario.TenantId });
            services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
            var roles = new Mock<ITenantRoleReader>();
            roles.Setup(reader => reader.ExistsAsync(scenario.TenantId, roleId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            roles.Setup(reader => reader.IsMemberAsync(scenario.TenantId, scenario.ReviewerId, roleId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(roles.Object);
            services.AddSingleton(database.CreateOptions<FinanceDbContext>());
            services.AddSingleton(database.CreateOptions<PlatformDbContext>());
            services.AddScoped<FinanceDbContext>();
            services.AddScoped<PlatformDbContext>();
            services.RemoveAll<IWorkflowFactsProvider>();
            services.AddScoped<InvoiceWorkflowFactsProvider>();
            services.AddScoped<IWorkflowFactsProvider>(provider => provider.GetRequiredService<InvoiceWorkflowFactsProvider>());
            services.AddScoped<IWorkflowActionHandler, InvoiceIssueWorkflowAction>();
            services.AddScoped<IWorkflowApprovalReader, WorkflowApprovalReader>();
            services.AddScoped<BillingService>();
            services.AddScoped<LedgerPostingService>();
            services.AddSingleton<FinanceMetrics>();
            services.AddSingleton<RecurrenceCalculator>();
            services.AddSingleton<ITaskActionHandlerCatalog, TaskActionHandlerCatalog>();
            services.AddScoped<ITaskService, WorkItemService>();
            services.AddScoped<WorkflowContinuationRequestedHandler>();
            services.AddSingleton<IWorkItemDispatcher, WorkItemDispatcher>();
            services.AddKeyedScoped<ITaskActionHandler, WorkflowContinuationTaskActionHandler>("workflow_continue");
        }

        async Task<WorkflowVersion> SeedInvoiceWorkflowAsync(AgentsDbContext context)
        {
            var workflow = new Workflow { TenantId = scenario.TenantId, Slug = "sql-issue", Name = "SQL invoice approval",
                State = WorkflowStates.Active, DefinitionSchemaVersion = 2 };
            var tool = new WorkflowToolParameters(InvoiceIssueWorkflowAction.ActionKey, 1,
                new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Fact: "invoice.id") }, new(roleId));
            JsonElement empty = JsonSerializer.SerializeToElement(new { });
            var definition = new WorkflowDefinition(2, "Workflow", workflow.Slug, workflow.Name, "",
                [new("start", "Trigger", "Start", empty),
                 new("issue", "Tool", "Issue invoice", JsonSerializer.SerializeToElement(tool, WorkflowDefinitionCodec.JsonOptions)),
                 new("done", "End", "Finish", JsonSerializer.SerializeToElement(new { outcome = "Completed" }))],
                [new("first", "start", "issue", 0, null), new("last", "issue", "done", 0, null)]);
            string json = WorkflowDefinitionCodec.Serialize(definition);
            var published = new WorkflowVersion { TenantId = scenario.TenantId, WorkflowId = workflow.Id, Tag = "v1", SchemaVersion = 2,
                DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json), PublishedAt = scenario.Clock.UtcNow, PublishedByUserId = scenario.AuthorId };
            workflow.PublishedVersionId = published.Id;
            context.Workflows.Add(workflow);
            context.WorkflowVersions.Add(published);
            await context.SaveChangesAsync();
            return published;
        }

        async Task<WorkflowContinuationRequestedEvent[]> DeliverDueWakesAsync(IServiceProvider services, Guid workflowRunId)
        {
            var outbox = await services.GetRequiredService<AgentsDbContext>().Set<OutboxMessage>().AsNoTracking()
                .Where(message => message.TenantId == scenario.TenantId && message.EventType == typeof(WorkflowContinuationRequestedEvent).FullName).ToListAsync();
            var due = outbox.Select(message => JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(message.Payload, OutboxSerialization.Options)!)
                .Where(wake => wake.RunId == workflowRunId && wake.NotBeforeUtc <= scenario.Clock.UtcNow).ToArray();
            var handler = services.GetRequiredService<WorkflowContinuationRequestedHandler>();
            foreach (WorkflowContinuationRequestedEvent wake in due) await handler.HandleAsync(wake);
            return due;
        }
    }

    // Stop workflow bookkeeping after Finance has committed, leaving both leases for a fresh worker to reclaim.
    private sealed class StopAfterInvoiceCommit : SaveChangesInterceptor
    {
        public const string Message = "Simulated worker loss after the SQL invoice commit.";
        private bool _committed;

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is FinanceDbContext finance && finance.ChangeTracker.Entries<Invoice>().Any(entry => entry.Entity.IssueOperationId.HasValue))
                _committed = true;
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_committed && eventData.Context is AgentsDbContext) throw new OperationCanceledException(Message);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
