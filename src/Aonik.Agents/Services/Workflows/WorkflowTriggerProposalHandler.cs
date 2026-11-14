using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerProposalHandler(string type, WorkflowTriggerService triggers) : IProposalHandler
{
    public string ProposalType => type;
    public Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
        => triggers.ApplyAsync(proposal.Id, type == WorkflowTriggerApprovalManifest.DisableType, cancellationToken);
}
