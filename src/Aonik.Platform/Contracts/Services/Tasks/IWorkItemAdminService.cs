using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Platform.Contracts.Services.Tasks;

/// <summary>
/// Admin-facing read over the current tenant's tasks (Spec 034), backing the Admin UI
/// task list. Kept separate from the cross-module <see cref="ITaskService"/> so the
/// programmatic scheduling contract stays free of admin-only listing concerns.
/// </summary>
public interface IWorkItemAdminService
{
    /// <summary>Lists the current tenant's tasks, optionally filtered by status, soonest-due first.</summary>
    Task<IReadOnlyList<TaskResponse>> ListAsync(string? status, int take, CancellationToken cancellationToken = default);
}
