using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationChannelState : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = "";
    public long Serial { get; set; }
    public Guid HeadSnapshotId { get; set; }
    public string BaselineSha256 { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string MirrorStatus { get; set; } = "disabled";
    public string MirrorCode { get; set; } = "not-configured";
    public long? MirroredSerial { get; set; }
    public Guid? MirrorClaimToken { get; set; }
    public DateTime? MirrorClaimExpiresAt { get; set; }
}
