using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorWorkflowUsageReader(PlatformDbContext database, ITenantProvider tenantProvider, IClock clock) : IWorkflowUsageReader
{
    public async Task<bool> HasActiveReferencesAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        return await database.ConnectorConnectionGrants.AsNoTracking().AnyAsync(grant => grant.TenantId == tenantId && grant.GranteeType == "Workflow"
                && grant.GranteeId == workflowId && grant.RevokedAt == null && (grant.ExpiresAt == null || grant.ExpiresAt > clock.UtcNow), cancellationToken)
            || await database.ConnectorConnectionRequests.AsNoTracking().AnyAsync(request => request.TenantId == tenantId && request.WorkflowId == workflowId
                && (request.Status == "Pending" || request.Status == "AwaitingConsent") && request.ExpiresAt > clock.UtcNow, cancellationToken);
    }
}
