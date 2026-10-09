using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.ContactEnquiries;

public class ContactEnquiry : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public string SubmissionHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public string StaffRecipientEmail { get; set; } = string.Empty;
    public string StaffDetailUrl { get; set; } = string.Empty;
    public List<ContactEnquiryAttachment> Images { get; set; } = [];
}
