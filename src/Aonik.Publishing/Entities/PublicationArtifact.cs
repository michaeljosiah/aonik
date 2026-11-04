using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationArtifact : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = "";
    public Guid OperationId { get; set; }
    public string AssetId { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
    public string MediaType { get; set; } = "";
    public string StorageLocationId { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime? ReadyAt { get; set; }
    public DateTime? PublicVerifiedAt { get; set; }
}
