using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderNote : AuditableEntity, ITenantScoped
{
    public Guid OrderNoteId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }

    private OrderNote() { }

    public OrderNote(Guid tenantId, Guid orderId, string note, Guid? createdByUserId = null)
    {
        OrderNoteId = Id;
        TenantId = tenantId;
        OrderId = orderId;
        Note = note;
        CreatedByUserId = createdByUserId;
    }

    public void UpdateNote(string note)
    {
        Note = note;
    }
}
