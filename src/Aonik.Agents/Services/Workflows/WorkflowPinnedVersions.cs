using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowPinnedVersions
{
    internal static async Task<WorkflowVersion> RequireAsync(AgentsDbContext database, Guid tenantId, WorkflowVersionPin pin,
        string definitionKind, CancellationToken cancellationToken = default)
    {
        WorkflowControlContracts.Pin(pin);
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == tenantId
            && version.Id == pin.VersionId && version.PublishedAt != null, cancellationToken) ?? throw new ArgumentException("A pinned published dependency is unavailable in this tenant.");
        if (!string.Equals(version.DefinitionHash, pin.DefinitionHash, StringComparison.Ordinal)
            || !await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == tenantId && workflow.Id == version.WorkflowId
                && workflow.State == WorkflowStates.Active && workflow.RetiredAt == null, cancellationToken)
            || WorkflowRunService.ReadDefinition(version).DefinitionKind != definitionKind)
            throw new ArgumentException("The dependency hash/kind differs or its workflow is retired.");
        return version;
    }
}
