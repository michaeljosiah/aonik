using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationSubmission : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CreatorPartyId { get; set; }
    public Guid OriginatingUserId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string ChannelKey { get; set; } = string.Empty;
    public string Status { get; set; } = SubmissionStatuses.AwaitingUpload;
    public Guid CurrentRevisionId { get; set; }
    public Guid? ActiveEditorialDecisionId { get; set; }
    public Guid? ActivePublicationOperationId { get; set; }
    public string? PublicationHoldReason { get; set; }
}

public static class SubmissionStatuses
{
    public const string AwaitingUpload = "awaiting-upload";
    public const string Uploaded = "uploaded";
    public const string Validating = "validating";
    public const string InReview = "in-review";
    public const string Held = "held";
    public const string ChangesRequested = "changes-requested";
    public const string Rejected = "rejected";
    public const string Approved = "approved";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string Withdrawn = "withdrawn";
}
