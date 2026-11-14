using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public class InvoiceWorkflowActionTests
{
    [Fact]
    public async Task Issue_Should_RecordTheApprovedOperationWithTheInvoice_AndReconcileAWorkerReplay()
    {
        using var host = new Host();
        WorkflowActionInvocation invocation = await host.Invocation();

        WorkflowActionResult first = await host.Action.ExecuteAsync(invocation);
        WorkflowActionResult replay = await host.Action.ExecuteAsync(invocation);

        first.Outcome.Should().Be("Issued");
        replay.Output.GetProperty("operationId").GetGuid().Should().Be(invocation.OperationId);
        host.Invoice.Status.Should().Be("Issued");
        host.Invoice.IssueOperationId.Should().Be(invocation.OperationId);
        host.Invoice.IssueApprovalRequestId.Should().Be(invocation.ApprovalRequestId!.Value);
        host.Invoice.IssueApprovedByUserId.Should().Be(host.ReviewerId);
        host.Approvals.Verify(reader => reader.RequireApprovedActorAsync(It.IsAny<WorkflowActionInvocation>(),
            InvoiceIssueWorkflowAction.ActionKey, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Issue_Should_RefuseAnotherOperation_WhenTheInvoiceWasAlreadyIssued()
    {
        using var host = new Host();
        WorkflowActionInvocation invocation = await host.Invocation();
        await host.Action.ExecuteAsync(invocation);

        Func<Task> another = async () => { await host.Action.ExecuteAsync(invocation with { OperationId = Guid.NewGuid() }); };

        await another.Should().ThrowAsync<InvalidStateException>();
        host.Invoice.IssueOperationId.Should().Be(invocation.OperationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Issue_Should_LeaveTheInvoiceDraft_WhenApprovalOrCurrentIssuePermissionIsMissing(bool revokePermission)
    {
        using var host = new Host();
        WorkflowActionInvocation invocation = await host.Invocation();
        if (revokePermission) host.CanIssue = false;
        else host.Approvals.Setup(reader => reader.RequireApprovedActorAsync(It.IsAny<WorkflowActionInvocation>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new PermissionDeniedException("Invoice.Issue"));

        Func<Task> issue = async () => { await host.Action.ExecuteAsync(invocation); };

        await issue.Should().ThrowAsync<PermissionDeniedException>();
        host.Invoice.Status.Should().Be("Draft");
        host.Invoice.IssueOperationId.Should().BeNull();
    }

    [Fact]
    public async Task Issue_Should_RefuseChangedFacts_AndRejectAnUnrelatedInvoiceArgument()
    {
        using var host = new Host();
        WorkflowActionInvocation invocation = await host.Invocation();
        host.Invoice.Total += 1;
        await host.Database.SaveChangesAsync();

        Func<Task> stale = async () => { await host.Action.ExecuteAsync(invocation); };
        await stale.Should().ThrowAsync<InvalidStateException>();
        Func<Task> other = async () => { await host.Action.ExecuteAsync(invocation with { Arguments = JsonSerializer.SerializeToElement(new { invoiceId = Guid.NewGuid() }) }); };
        await other.Should().ThrowAsync<ArgumentException>();
        host.Invoice.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Facts_Should_UseStoredDecimalStrings_AndNeverInventRiskOrDuplicateClearance()
    {
        using var host = new Host();
        await host.Database.SaveChangesAsync();

        WorkflowFactsSnapshot facts = (await host.Facts.LoadAsync(host.Invoice.Id, new("User", host.UserId)))!;

        facts.Facts["invoice.total"].GetString().Should().Be("5000.0001");
        facts.Facts["invoice.currency"].GetString().Should().Be("GBP");
        facts.Facts.Should().NotContainKey("invoice.hasRiskConcern").And.NotContainKey("invoice.isDuplicate");
        host.Invoice.Status = "Cancelled";
        await host.Database.SaveChangesAsync();
        (await host.Facts.LoadAsync(host.Invoice.Id, new("User", host.UserId)))!.BlockReason.Should().Be("InvoiceNotDraft");
    }

    private sealed class Host : IDisposable
    {
        private readonly FinanceMetrics _metrics = new();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid ReviewerId { get; } = Guid.NewGuid();
        public bool CanIssue { get; set; } = true;
        public FinanceDbContext Database { get; }
        public Invoice Invoice { get; }
        public InvoiceWorkflowFactsProvider Facts { get; }
        public InvoiceIssueWorkflowAction Action { get; }
        public Mock<IWorkflowApprovalReader> Approvals { get; } = new();

        public Host()
        {
            var tenant = new Tenant();
            Database = new(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase($"InvoiceWorkflow_{Guid.NewGuid()}").Options, tenant);
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(service => service.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string permission, CancellationToken _) => permission != "Invoice.Issue" || CanIssue);
            var users = new Mock<IUserDirectoryReader>();
            users.Setup(reader => reader.GetByIdsAsync(tenant.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new(UserId, null, "Active"), new(ReviewerId, null, "Active")]);
            Facts = new(Database, tenant, permissions.Object, users.Object);
            Approvals.Setup(reader => reader.RequireApprovedActorAsync(It.IsAny<WorkflowActionInvocation>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ReviewerId);
            var billing = new BillingService(Database, tenant, permissions.Object, Mock.Of<ICurrentUserProvider>(), _metrics,
                new LedgerPostingService(Database), Approvals.Object);
            Action = new(billing, Database, tenant, Facts);
            Invoice = new() { TenantId = tenant.Id, CustomerAccountId = Guid.NewGuid(), Total = 5000.0001m, Currency = "gbp", Status = "Draft", ProvenanceJson = "{}" };
            Database.Invoices.Add(Invoice);
        }

        public async Task<WorkflowActionInvocation> Invocation()
        {
            await Database.SaveChangesAsync();
            WorkflowFactsSnapshot facts = (await Facts.LoadAsync(Invoice.Id, new("User", UserId)))!;
            return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new("User", UserId, UserId), "Invoice", Invoice.Id,
                facts.SubjectVersion, WorkflowFactsHash.Compute(facts), JsonSerializer.SerializeToElement(new { invoiceId = Invoice.Id }), Guid.NewGuid());
        }
        public void Dispose() { Database.Dispose(); _metrics.Dispose(); }
    }
    private sealed class Tenant : ITenantProvider
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid GetCurrentTenantId() => Id;
        public bool TryGetCurrentTenantId(out Guid id) { id = Id; return true; }
    }
}
