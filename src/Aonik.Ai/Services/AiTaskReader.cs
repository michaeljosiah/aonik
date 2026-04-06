using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// Cross-module reader for AI task definitions. Implements the SharedKernel
/// <see cref="IAiTaskReader"/> contract so that the Agents module (playground)
/// can resolve task templates without a direct project reference.
/// </summary>
internal sealed class AiTaskReader : IAiTaskReader
{
    private readonly AiDbContext _dbContext;

    public AiTaskReader(AiDbContext dbContext) => _dbContext = dbContext;

    public async Task<AiTaskSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.AiTasks
            .AsNoTracking()
            .Where(t => t.Id == id && t.IsPublished)
            .FirstOrDefaultAsync(cancellationToken);

        if (task is null)
            return null;

        return new AiTaskSnapshot(
            task.Id,
            task.UseCase,
            task.DisplayName,
            string.IsNullOrEmpty(task.SystemTemplate) ? null : task.SystemTemplate,
            string.IsNullOrEmpty(task.UserTemplate) ? null : task.UserTemplate,
            string.IsNullOrEmpty(task.DeveloperTemplate) ? null : task.DeveloperTemplate,
            string.IsNullOrEmpty(task.VariablesSchemaJson) ? null : task.VariablesSchemaJson);
    }
}
