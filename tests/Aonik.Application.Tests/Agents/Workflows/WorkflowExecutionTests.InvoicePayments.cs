using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task InvoicePayment_Should_WaitForASeparateHighHumanProposal_ThenCreateOneOrderLinkedIntent_WithSourceAudit()
    {
        using var flow = new PaymentFlow(); await flow.StartAsync();
        using (IServiceScope scope = flow.Host.Scope())
        {
            var agents = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); var finance = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            ToolApprovalRequest request = await flow.RequestAsync(scope);
            request.RiskTier.Should().Be("High"); request.PreviewJson.Should().Contain("250").And.Contain(flow.Financial.OrderId!.Value.ToString()).And.Contain(flow.Financial.AiRunId.ToString());
            (await agents.Proposals.SingleAsync(proposal => proposal.Id == request.ProposalId)).Status.Should().Be(ProposalStatus.Proposed);
            (await finance.PaymentIntents.CountAsync()).Should().Be(0);
            Func<Task> bypass = () => scope.ServiceProvider.GetRequiredService<PaymentService>().CreateInvoicePaymentFromProposalAsync(request.ProposalId!.Value);
            await bypass.Should().ThrowAsync<PermissionDeniedException>();
            Func<Task> inline = () => scope.ServiceProvider.GetRequiredService<InvoicePaymentIntentWorkflowAction>().ExecuteAsync(null!);
            await inline.Should().ThrowAsync<PermissionDeniedException>();
        }
        await flow.Financial.DecideAsync(flow.Host, flow.ChildId, true); flow.Host.Actor.Authenticated = false;
        (await flow.Host.Continue(flow.ChildId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await flow.Host.Continue(flow.RootId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await flow.Host.Continue(flow.ChildId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = flow.Host.Scope(); var services = final.ServiceProvider;
        var payment = await services.GetRequiredService<FinanceDbContext>().PaymentIntents.SingleAsync();
        payment.Status.Should().Be("Pending"); payment.OrderId.Should().Be(flow.Financial.OrderId!.Value); payment.Amount.Should().Be(250m);
        payment.Currency.Should().Be("GBP"); payment.InvoiceId.Should().NotBeNull(); payment.PayerPartyId.Should().NotBeNull(); payment.PayeePartyId.Should().NotBeNull();
        payment.ProvenanceJson.Should().Contain(flow.Financial.AiRunId.ToString()).And.Contain("approvalRequestId").And.Contain("orderHash");
        (await services.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
        (await services.GetRequiredService<FinanceDbContext>().JournalEntries.CountAsync()).Should().Be(0);
        var step = await services.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.SingleAsync(step => step.RunId == flow.ChildId && step.ActionKey == InvoicePaymentIntentWorkflowAction.ActionKey);
        step.AiRunId.Should().Be(flow.Financial.AiRunId);
        (await services.GetRequiredService<AgentsDbContext>().Proposals.SingleAsync(proposal => proposal.Id == step.ProposalId)).AiRunId.Should().Be(flow.Financial.AiRunId);
    }

    [Fact]
    public async Task InvoicePayment_Should_LeaveTheInvoiceIssuedAndCreateNoIntent_WhenTheHighProposalIsRejected()
    {
        using var flow = new PaymentFlow(); await flow.StartAsync();
        await flow.Financial.DecideAsync(flow.Host, flow.ChildId, false); flow.Host.Actor.Authenticated = false;
        await flow.Host.Continue(flow.ChildId);
        using IServiceScope scope = flow.Host.Scope();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(0);
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().Proposals.SingleAsync(proposal => proposal.ProposalType == InvoicePaymentIntentWorkflowAction.ProposalType)).Status.Should().Be(ProposalStatus.Rejected);
    }

    [Theory]
    [InlineData("wrong-reviewer")]
    [InlineData("revoked-actor")]
    [InlineData("changed-invoice")]
    [InlineData("cancelled")]
    public async Task InvoicePayment_Should_RefuseLostReviewOrExecutionAuthority_BeforeAnyIntent(string invalid)
    {
        using var flow = new PaymentFlow(); await flow.StartAsync(); flow.Host.Actor.Authenticated = true; flow.Host.Actor.Id = flow.Host.Reviewer;
        using IServiceScope scope = flow.Host.Scope(); var services = scope.ServiceProvider; ToolApprovalRequest request = await flow.RequestAsync(scope);
        if (invalid == "wrong-reviewer") flow.Host.Actor.Id = Guid.NewGuid();
        if (invalid == "revoked-actor")
        {
            var platform = services.GetRequiredService<PlatformDbContext>(); (await platform.AutomationIdentities.SingleAsync()).Status = "Disabled"; await platform.SaveChangesAsync();
        }
        if (invalid == "changed-invoice")
        {
            var finance = services.GetRequiredService<FinanceDbContext>(); (await finance.Invoices.SingleAsync()).Total = 251m; await finance.SaveChangesAsync();
        }
        if (invalid == "cancelled")
        {
            var runs = services.GetRequiredService<IWorkflowRunService>(); var run = (await runs.GetAsync(flow.ChildId))!; await runs.CancelAsync(run.Id, new(run.ExpectedVersion));
        }
        try
        {
            ToolApprovalDecisionResult result = await services.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));
            result.Outcome.Should().NotBe(ToolApprovalDecisionOutcome.Approved);
        }
        catch (WorkflowConflictException) when (invalid == "changed-invoice") { }
        (await services.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvoicePayment_Should_StopAtTheExistingExpiryWake_WithoutDispatchingTheUnapprovedProposal(bool decisionArrivedFirst)
    {
        using var flow = new PaymentFlow(); await flow.StartAsync();
        using (IServiceScope scope = flow.Host.Scope()) flow.Host.Clock.UtcNow = (await flow.RequestAsync(scope)).ExpiresAt.AddSeconds(1);
        if (decisionArrivedFirst)
        {
            flow.Host.Actor.Authenticated = true; flow.Host.Actor.Id = flow.Host.Reviewer;
            using IServiceScope scope = flow.Host.Scope(); var request = await flow.RequestAsync(scope);
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Expired);
            flow.Host.Actor.Authenticated = false;
        }

        var expired = await flow.Host.Continue(flow.ChildId);

        expired.Status.Should().Be(WorkflowRunStatuses.Completed); expired.Outcome.Should().Be("ApprovalExpired");
        using IServiceScope final = flow.Host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(0);
        (await flow.RequestAsync(final)).Status.Should().Be(ToolApprovalRequestStatus.Expired);
    }

    [Theory]
    [InlineData("order-amount")]
    [InlineData("order-cancelled")]
    [InlineData("payload")]
    public async Task InvoicePayment_Should_FailTheApprovedProposalWithoutAnIntent_WhenItsReviewedOrderOrPayloadChanges(string invalid)
    {
        using var flow = new PaymentFlow(); await flow.StartAsync(); flow.Host.Actor.Authenticated = true; flow.Host.Actor.Id = flow.Host.Reviewer;
        using IServiceScope scope = flow.Host.Scope(); var services = scope.ServiceProvider; ToolApprovalRequest request = await flow.RequestAsync(scope);
        var agents = services.GetRequiredService<AgentsDbContext>(); var finance = services.GetRequiredService<FinanceDbContext>();
        if (invalid == "payload") { (await agents.Proposals.SingleAsync(proposal => proposal.Id == request.ProposalId)).PayloadJson = "{\"invoiceId\":\"" + Guid.NewGuid() + "\"}"; await agents.SaveChangesAsync(); }
        else { var order = await finance.Orders.SingleAsync(); if (invalid == "order-amount") order.AmountIn++; else order.Status = "Cancelled"; await finance.SaveChangesAsync(); }

        Func<Task> approve = () => services.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));

        if (invalid == "payload") await approve.Should().ThrowAsync<PermissionDeniedException>();
        else await approve.Should().ThrowAsync<InvalidStateException>();
        (await finance.PaymentIntents.CountAsync()).Should().Be(0);
        (await agents.Proposals.AsNoTracking().SingleAsync(proposal => proposal.Id == request.ProposalId)).Status.Should().Be(ProposalStatus.Failed);
    }

    [Fact]
    public async Task InvoicePayment_Should_RecoverItsDomainReceiptAfterDispatchLoss_WithoutReapprovingTheFailedHighProposal()
    {
        using var flow = new PaymentFlow { CrashAfterCommit = true }; await flow.StartAsync();
        flow.Host.Actor.Authenticated = true; flow.Host.Actor.Id = flow.Host.Reviewer;
        Guid proposalId;
        using (IServiceScope scope = flow.Host.Scope())
        {
            ToolApprovalRequest request = await flow.RequestAsync(scope); proposalId = request.ProposalId!.Value;
            Func<Task> approve = () => scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));
            await approve.Should().ThrowAsync<InvalidOperationException>().WithMessage("Simulated payment worker loss after commit.");
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(1);
        }
        flow.Host.Actor.Authenticated = false;

        (await flow.Host.Continue(flow.ChildId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await flow.Host.Continue(flow.RootId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await flow.Host.Continue(flow.ChildId)).Status.Should().Be(WorkflowRunStatuses.Completed);

        using IServiceScope final = flow.Host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().Proposals.SingleAsync(proposal => proposal.Id == proposalId)).Status.Should().Be(ProposalStatus.Failed);
        Func<Task> redispatch = () => final.ServiceProvider.GetRequiredService<PaymentService>().CreateInvoicePaymentFromProposalAsync(proposalId);
        await redispatch.Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task InvoicePayment_Should_ReadItsCommittedReceipt_WhenTheProcessLostTheProposalCompletionAcknowledgement()
    {
        using var flow = new PaymentFlow(); await flow.StartAsync(); await flow.Financial.DecideAsync(flow.Host, flow.ChildId, true);
        using (IServiceScope scope = flow.Host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); var request = await flow.RequestAsync(scope);
            var proposal = await database.Proposals.SingleAsync(proposal => proposal.Id == request.ProposalId);
            // Reproduce a process loss after the separate Finance commit and before Agents stores its dispatcher receipt.
            proposal.Status = ProposalStatus.Approved; proposal.ExecutionResultJson = null; await database.SaveChangesAsync();
        }
        flow.Host.Actor.Authenticated = false;

        (await flow.Host.Continue(flow.ChildId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await flow.Host.Continue(flow.RootId)).Status.Should().Be(WorkflowRunStatuses.Completed);

        using IServiceScope final = flow.Host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().PaymentIntents.CountAsync()).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.SingleAsync(step => step.RunId == flow.ChildId
            && step.ActionKey == InvoicePaymentIntentWorkflowAction.ActionKey)).ResultJson.Should().Contain("paymentIntentId");
    }

    private sealed class PaymentFlow : IDisposable
    {
        public FinancialFixture Financial { get; } = new() { OrderId = Guid.NewGuid(), IncludePayment = true };
        public Host Host { get; } public Guid RootId { get; private set; } public Guid ChildId { get; private set; }
        public bool CrashAfterCommit { get; set; }
        public PaymentFlow()
        {
            Host = new(services =>
            {
                Financial.Configure(services); services.AddScoped<PaymentService>(); services.AddScoped<InvoicePaymentIntentWorkflowAction>();
                services.AddScoped<IWorkflowActionHandler>(provider => provider.GetRequiredService<InvoicePaymentIntentWorkflowAction>());
                services.AddScoped<InvoicePaymentIntentProposalHandler>();
                services.AddKeyedScoped<IProposalHandler>(InvoicePaymentIntentWorkflowAction.ProposalType,
                    (provider, _) => new PaymentCrashHandler(this, provider.GetRequiredService<InvoicePaymentIntentProposalHandler>()));
            });
        }
        public async Task StartAsync()
        {
            WorkflowVersionPin child = await Host.PublishChild(draft =>
            {
                var reviewer = JsonSerializer.Deserialize<WorkflowToolParameters>(draft.Nodes.Single(node => node.ClientId == "issue").ParamsJson, WorkflowDefinitionCodec.JsonOptions)!.Reviewer;
                string payment = JsonSerializer.Serialize(new WorkflowToolParameters(InvoicePaymentIntentWorkflowAction.ActionKey, 1,
                    new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Fact: "invoice.id") }, reviewer), WorkflowDefinitionCodec.JsonOptions);
                return draft with { Nodes = [.. draft.Nodes.Where(node => node.ClientId is "start" or "issue" or "done"), new("payment", "Tool", "Review payment intent", "", "", 3, 0, payment)],
                    Edges = [new("start", "issue", 0, ""), new("issue", "payment", 0, ""), new("payment", "done", 0, "")] };
            });
            var intake = await Host.PublishChild(draft => Financial.Definition(draft, child), "document-intake");
            await Financial.SeedAsync(Host); RootId = await Financial.StartAsync(Host, intake, child, true); Host.Actor.Authenticated = false;
            (await Host.Continue(RootId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
            await Financial.DecideAsync(Host, RootId, true); Host.Actor.Authenticated = false;
            (await Host.Continue(RootId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
            using (IServiceScope scope = Host.Scope()) ChildId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == RootId)).Id;
            (await Host.Continue(ChildId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
            await Financial.DecideAsync(Host, ChildId, true); Host.Actor.Authenticated = false;
            var waiting = await Host.Continue(ChildId); waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, waiting.ErrorCode);
        }
        public Task<ToolApprovalRequest> RequestAsync(IServiceScope scope)
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            return database.ToolApprovalRequests.SingleAsync(request => request.RiskTier == "High" && database.WorkflowStepRuns.Any(step => step.RunId == ChildId && step.Id == request.WorkflowStepRunId));
        }
        public void Dispose() => Host.Dispose();
    }
    private sealed class PaymentCrashHandler(PaymentFlow flow, InvoicePaymentIntentProposalHandler inner) : IProposalHandler
    {
        public string ProposalType => inner.ProposalType;
        public async Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
        {
            ProposalHandlerResult result = await inner.HandleAsync(proposal, cancellationToken);
            if (flow.CrashAfterCommit) { flow.CrashAfterCommit = false; throw new InvalidOperationException("Simulated payment worker loss after commit."); }
            return result;
        }
    }
}
