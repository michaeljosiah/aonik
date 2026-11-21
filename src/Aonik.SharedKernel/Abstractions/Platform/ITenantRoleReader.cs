namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>Tenant-scoped role checks for server-selected workflow reviewers and connection grants.</summary>
public interface ITenantRoleReader
{
    Task<bool> ExistsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
