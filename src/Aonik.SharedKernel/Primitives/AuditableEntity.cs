namespace Aonik.SharedKernel.Primitives;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected AuditableEntity()
    {
        CreatedAt = DateTime.UtcNow;
    }

    protected AuditableEntity(Guid id) : base(id)
    {
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsCreated(Guid? createdBy = null)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public void MarkAsUpdated(Guid? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void MarkAsDeleted(Guid? deletedBy = null)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
