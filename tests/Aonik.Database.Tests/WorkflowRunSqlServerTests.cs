using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class WorkflowRunSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task Admission_Should_DeduplicateConcurrentStarts_AndCommitOnlyOneWakeup()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid subjectId = Guid.NewGuid();
        Guid versionId;
        await using (AgentsDbContext seed = Context(tenantId))
        {
            var workflows = new WorkflowService(seed, new TestTenantProvider(tenantId), new Clock(),
                new(new TestTenantProvider(tenantId), new TestCurrentUserProvider(actorId), new AllowAllPermissionService()));
            await workflows.CreateAsync(new("sql-invoice-route", "Invoice routing", "", "Draft", "v0.1", false, "", null, [],
                [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")],
                [new("start", "end", 0, "")], null));
            Workflow workflow = await seed.Workflows.SingleAsync();
            WorkflowVersion version = await seed.WorkflowVersions.SingleAsync();
            version.PublishedAt = DateTime.UtcNow;
            version.PublishedByUserId = actorId;
            workflow.PublishedVersionId = version.Id;
            workflow.State = "Active";
            await seed.SaveChangesAsync();
            versionId = version.Id;
        }
        var request = new StartWorkflowRunRequest(versionId, "Invoice", subjectId, "same-admission");
        Task<Guid>[] starts = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using AgentsDbContext context = Context(tenantId);
            try { return (await Service(context, tenantId, actorId, subjectId).StartAsync("sql-invoice-route", request)).Id; }
            catch (WorkflowConflictException)
            {
                await using AgentsDbContext retry = Context(tenantId);
                return (await Service(retry, tenantId, actorId, subjectId).StartAsync("sql-invoice-route", request)).Id;
            }
        }).ToArray();

        Guid[] ids = await Task.WhenAll(starts);

        ids.Distinct().Should().ContainSingle();
        await using AgentsDbContext verify = Context(tenantId);
        (await verify.WorkflowRuns.CountAsync()).Should().Be(1);
        (await verify.Set<OutboxMessage>().CountAsync(message => message.TenantId == tenantId)).Should().Be(1);
        (await verify.Workflows.SingleAsync()).TriggerCount.Should().Be(1);
        Func<Task> changed = async () => { await Service(verify, tenantId, actorId, subjectId)
            .StartAsync("sql-invoice-route", request with { SubjectId = Guid.NewGuid() }); };
        await changed.Should().ThrowAsync<WorkflowConflictException>();
    }

    [SkippableFact]
    public async Task RunLeaseAndStepIdentity_Should_FenceConcurrentWorkers_AndRejectDuplicateEffects()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        Guid runId;
        await using (AgentsDbContext seed = Context(tenantId))
        {
            var run = new WorkflowRun { TenantId = tenantId, WorkflowId = Guid.NewGuid(), Mode = "Live", Status = "Queued", StartedAt = DateTime.UtcNow };
            seed.WorkflowRuns.Add(run);
            await seed.SaveChangesAsync();
            runId = run.Id;
        }
        await using AgentsDbContext first = Context(tenantId);
        await using AgentsDbContext second = Context(tenantId);
        WorkflowRun one = await first.WorkflowRuns.SingleAsync(run => run.Id == runId);
        WorkflowRun two = await second.WorkflowRuns.SingleAsync(run => run.Id == runId);
        one.LeaseId = Guid.NewGuid();
        one.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2);
        two.LeaseId = Guid.NewGuid();
        two.LeaseExpiresAt = one.LeaseExpiresAt;
        await first.SaveChangesAsync();
        Func<Task> staleClaim = async () => await second.SaveChangesAsync();
        await staleClaim.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var step = new WorkflowStepRun { TenantId = tenantId, RunId = runId, NodeKey = "issue", OperationId = Guid.NewGuid() };
        first.WorkflowStepRuns.Add(step);
        await first.SaveChangesAsync();
        await using AgentsDbContext duplicate = Context(tenantId);
        duplicate.WorkflowStepRuns.Add(new() { TenantId = tenantId, RunId = runId, NodeKey = "issue", OperationId = Guid.NewGuid() });
        Func<Task> duplicateInvocation = async () => await duplicate.SaveChangesAsync();
        await duplicateInvocation.Should().ThrowAsync<DbUpdateException>();
        await using AgentsDbContext duplicateOperation = Context(tenantId);
        duplicateOperation.WorkflowStepRuns.Add(new() { TenantId = tenantId, RunId = runId, NodeKey = "other", OperationId = step.OperationId });
        Func<Task> repeatedOperation = async () => await duplicateOperation.SaveChangesAsync();
        await repeatedOperation.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task InvoiceIssue_Should_CommitItsReceiptWithTheState_AndRejectAStaleConcurrentOperation()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid reviewerId = Guid.NewGuid();
        var tenant = new TestTenantProvider(tenantId);
        await using var first = new FinanceDbContext(database.CreateOptions<FinanceDbContext>(), tenant);
        await using var stale = new FinanceDbContext(database.CreateOptions<FinanceDbContext>(), tenant);
        var invoice = new Invoice { TenantId = tenantId, CustomerAccountId = Guid.NewGuid(), Currency = "GBP", Total = 42.25m, Status = "Draft", ProvenanceJson = "{}" };
        first.Invoices.Add(invoice);
        await first.SaveChangesAsync();
        await stale.Invoices.SingleAsync(item => item.Id == invoice.Id);
        WorkflowFactsSnapshot facts = InvoiceWorkflowFactsProvider.FromInvoice(invoice);
        var invocation = new WorkflowActionInvocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new("User", actorId, actorId),
            "Invoice", invoice.Id, facts.SubjectVersion, WorkflowFactsHash.Compute(facts), JsonSerializer.SerializeToElement(new { invoiceId = invoice.Id }), Guid.NewGuid());
        var approvals = new Mock<IWorkflowApprovalReader>();
        approvals.Setup(reader => reader.RequireApprovedActorAsync(It.IsAny<WorkflowActionInvocation>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(reviewerId);
        using var metrics = new FinanceMetrics();
        BillingService Billing(FinanceDbContext db) => new(db, tenant, new AllowAllPermissionService(), new TestCurrentUserProvider(actorId), metrics,
            new LedgerPostingService(db), approvals.Object);

        await Billing(first).IssueInvoiceForWorkflowAsync(invocation);
        Func<Task> concurrent = async () => { await Billing(stale).IssueInvoiceForWorkflowAsync(invocation with { OperationId = Guid.NewGuid() }); };

        await concurrent.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var verify = new FinanceDbContext(database.CreateOptions<FinanceDbContext>(), tenant);
        Invoice stored = await verify.Invoices.SingleAsync(item => item.Id == invoice.Id);
        stored.Status.Should().Be("Issued");
        stored.IssueOperationId.Should().Be(invocation.OperationId);
        stored.IssueApprovalRequestId.Should().Be(invocation.ApprovalRequestId!.Value);
        stored.IssueApprovedByUserId.Should().Be(reviewerId);
    }

    private AgentsDbContext Context(Guid tenantId) => new(database.CreateOptions<AgentsDbContext>(), new TestTenantProvider(tenantId));
    private static WorkflowRunService Service(AgentsDbContext db, Guid tenantId, Guid userId, Guid subjectId)
    {
        var tenant = new TestTenantProvider(tenantId);
        var users = new Mock<IUserDirectoryReader>();
        users.Setup(reader => reader.GetByIdsAsync(tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new(userId, null, "Active")]);
        var settings = new Mock<ITenantSettingStore>();
        settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, tenantId, It.IsAny<CancellationToken>())).ReturnsAsync("true");
        var permissions = new AllowAllPermissionService();
        return new(db, tenant, new(tenant, new TestCurrentUserProvider(userId), permissions),
            new(tenant, permissions, users.Object, settings.Object, new ConfigurationBuilder().Build(), [new Facts(subjectId)]), new Clock());
    }
    private sealed class Facts(Guid subjectId) : IWorkflowFactsProvider
    {
        public string SubjectType => "Invoice";
        public string Schema => "invoice-approval.v1";
        public Task<WorkflowFactsSnapshot?> LoadAsync(Guid id, WorkflowActor actor, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowFactsSnapshot?>(id == subjectId ? new(Schema, new Dictionary<string, JsonElement>
            {
                ["invoice.id"] = JsonSerializer.SerializeToElement(id), ["invoice.total"] = JsonSerializer.SerializeToElement("42.25"),
                ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP"), ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"),
            }, "version-1") : null);
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
