using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Notifications.Entities;

public class Notification : AuditableEntity
{
    public Guid NotificationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public string RecipientRef { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime? SentAt { get; private set; }

    private Notification() { }

    public Notification(Guid tenantId, string channel, string templateKey, string recipientRef, string payloadJson)
    {
        NotificationId = Id;
        TenantId = tenantId;
        Channel = channel;
        TemplateKey = templateKey;
        RecipientRef = recipientRef;
        PayloadJson = payloadJson;
        Status = "Pending";
    }

    public void MarkAsSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = "Failed";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
