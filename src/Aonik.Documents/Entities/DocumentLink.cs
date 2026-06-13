using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

/// <summary>
/// A thin association from a <see cref="Document"/> to a Simi target (Spec 046):
/// a CareEntity (Spec 043), a PaymentLog (Spec 045), or a commitment (Spec 044).
/// The target is an opaque Guid into PersonalFinance — NO cross-module FK; the
/// link to Document is a real same-module FK. One document may have several links.
/// </summary>
public class DocumentLink : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>→ Document.Id (same module → real FK).</summary>
    public Guid DocumentId { get; set; }

    /// <summary>careEntity | paymentLog | commitment.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Opaque Guid into PersonalFinance.</summary>
    public Guid TargetId { get; set; }
}
