using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Finance.Services.Payments;

internal sealed class InvoicePaymentIntentProposalHandler(PaymentService payments) : IProposalHandler
{
    public string ProposalType => InvoicePaymentIntentWorkflowAction.ProposalType;
    public async Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
    {
        WorkflowPaymentReceipt result = await payments.CreateInvoicePaymentFromProposalAsync(proposal.Id, cancellationToken);
        return new(true, "PaymentIntent", result.PaymentIntentId, "The approved Order-linked pending payment intent was created.", result.Result.Output, result.Result.AiRunId);
    }
}
