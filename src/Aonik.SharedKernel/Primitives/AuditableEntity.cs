using System.ComponentModel.DataAnnotations;

namespace Aonik.SharedKernel.Primitives;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token. On SQL Server this is mapped to the native
    /// <c>rowversion</c> type, which the database auto-increments on every INSERT
    /// and UPDATE. EF Core includes this value in the WHERE clause of UPDATE/DELETE
    /// commands to detect concurrent modifications.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
