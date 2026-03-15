using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

public class NotificationPreference : AuditableEntity
{
    public Guid PartyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool NewBillsPush { get; set; } = true;
    public bool BillUpdatesPush { get; set; } = true;
    public bool BillAssistPush { get; set; }
    public bool MbaMessagesPush { get; set; } = true;
    public bool OrgMessagesPush { get; set; } = true;
    public bool FriendsMessagesPush { get; set; }
    public bool NewBillsEmail { get; set; } = true;
    public bool BillUpdatesEmail { get; set; } = true;
    public bool BillAssistEmail { get; set; }
    public bool MbaMessagesEmail { get; set; } = true;
    public bool OrgMessagesEmail { get; set; } = true;
    public Party Party { get; set; } = null!;
}
