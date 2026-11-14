using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors;

internal sealed class ConnectorMutationProposalHandler(string type, IConnectorMutationService mutations) : IProposalHandler
{
    public string ProposalType => type;
    public async Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
    {
        if (proposal.ProposalType != type) throw new InvalidOperationException("The connector proposal handler does not match this action.");
        var receipt = await mutations.ExecuteProposalAsync(proposal.Id, cancellationToken);
        return new(true, "ConnectorMutation", proposal.Id, "The approved connector action has a recorded provider receipt.", receipt);
    }
}
