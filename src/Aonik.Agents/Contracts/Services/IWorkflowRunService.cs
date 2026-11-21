using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Contracts.Services;

public interface IWorkflowRunService
{
    Task<WorkflowRunDetail> StartAsync(string slug, StartWorkflowRunRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowRunDetail?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<WorkflowRunDetail> ResumeAsync(Guid runId, WorkflowRunCommand command, CancellationToken cancellationToken = default);
    Task<WorkflowRunDetail> CancelAsync(Guid runId, WorkflowRunCommand command, CancellationToken cancellationToken = default);
}
