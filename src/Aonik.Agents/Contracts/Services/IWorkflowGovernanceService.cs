using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Contracts.Services;

public interface IWorkflowGovernanceService
{
    Task<WorkflowGovernanceRequestResult> RequestAsync(
        WorkflowGovernanceCommand command, bool retiring, CancellationToken cancellationToken = default);
}
