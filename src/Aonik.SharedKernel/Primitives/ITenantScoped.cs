namespace Aonik.SharedKernel.Primitives;

/// <summary>
/// Marker interface for entities that are scoped to a specific tenant.
/// Entities implementing this interface will have automatic tenant-based query filters applied.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The unique identifier of the tenant that owns this entity.
    /// </summary>
    Guid TenantId { get; }
}
