using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationUploadPart : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public int Number { get; set; }
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool Received { get; set; }
}
