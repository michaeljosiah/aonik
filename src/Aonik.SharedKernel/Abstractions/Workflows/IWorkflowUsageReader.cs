namespace Aonik.SharedKernel.Abstractions.Workflows;

/// <summary>Owning modules report concrete active references before a draft can be removed.</summary>
public interface IWorkflowUsageReader
{
    Task<bool> HasActiveReferencesAsync(Guid workflowId, CancellationToken cancellationToken = default);
}
