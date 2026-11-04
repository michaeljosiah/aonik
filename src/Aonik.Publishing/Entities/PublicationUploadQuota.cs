using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationUploadQuota : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    // Empty denotes the publisher total; otherwise the creator's logical retained-byte allowance.
    public Guid OwnerPartyId { get; set; }
    public long ReservedBytes { get; set; }
    public int ReservedPackages { get; set; }
}
