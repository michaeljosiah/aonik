using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationNamespace : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public Guid CreatorPartyId { get; set; }
    // Set only by the future audited catalogue import/publication service, never a creator request.
    public string? ApprovedBaseFingerprint { get; set; }
    public int LastApprovedPartNumber { get; set; }
}
