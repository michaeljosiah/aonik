using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

public interface IAiTaskService
{
    Task<IReadOnlyList<AiTaskResponse>> ListAsync(string? category = null, CancellationToken ct = default);
    Task<AiTaskDetailResponse?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<AiTaskResponse> CreateAsync(CreateAiTaskRequest request, CancellationToken ct = default);
    Task<AiTaskResponse> UpdateAsync(Guid id, UpdateAiTaskRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Resets the task's prompt templates back to the hard-coded defaults
    /// defined in <c>AiTaskSeedService</c> (matched by UseCase). Overwrites
    /// SystemTemplate and UserTemplate; clears DeveloperTemplate.
    /// Other fields (category, model route, schema, etc.) are preserved.
    /// </summary>
    Task<AiTaskResponse> ResetPromptAsync(Guid id, CancellationToken ct = default);
}
