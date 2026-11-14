using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowGovernanceProposalHandler(string proposalType, WorkflowGovernanceService governance) : IProposalHandler
{
    public string ProposalType => proposalType;
    public Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
        => governance.ApplyAsync(proposal.Id, proposalType == WorkflowToolApprovalManifest.RetireType, cancellationToken);
}
