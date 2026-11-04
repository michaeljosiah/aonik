using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class EditorialReviewSnapshot : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid ValidationReportId { get; set; }
    public string ReviewDigest { get; set; } = string.Empty;
    public int DigestVersion { get; set; } = 1;
    public string MaterialJson { get; set; } = string.Empty;
    public string PolicyJson { get; set; } = string.Empty;
}
