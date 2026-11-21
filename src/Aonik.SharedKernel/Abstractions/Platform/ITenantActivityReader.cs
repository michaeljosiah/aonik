namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>Reads the current status of the tenant before deferred work can exercise authority.</summary>
public interface ITenantActivityReader
{
    Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
