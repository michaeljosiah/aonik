using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationCatalogueSnapshot : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = "";
    public long Serial { get; set; }
    public Guid? PreviousSnapshotId { get; set; }
    public Guid OperationId { get; set; }
    public string Json { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
}
