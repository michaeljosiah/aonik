using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Contracts.Services;

public interface IWorkflowTriggerService
{
    Task<IReadOnlyList<WorkflowTriggerDetail>> ListAsync(string slug, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerDetail?> GetAsync(string slug, Guid bindingId, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerDetail> SaveAsync(string slug, Guid? bindingId, SaveWorkflowTriggerRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string slug, Guid bindingId, string expectedVersion, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerValidation> ValidateAsync(string slug, Guid bindingId, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerGovernanceResult> RequestAsync(string slug, Guid bindingId, WorkflowTriggerCommand command, bool disabling, CancellationToken cancellationToken = default);
}
