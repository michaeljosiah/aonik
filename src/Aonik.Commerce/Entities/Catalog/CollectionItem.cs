using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>One ranked member of a <see cref="Collection"/> (Spec 070 §5). Anemic.
/// <see cref="ProductId"/> is validated against the tenant's products at authoring time; a draft
/// product may be staged here — Active is enforced by the public read, not by membership.</summary>
public class CollectionItem : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Order within the collection. Unique among live rows — curated order must be
    /// deterministic, so ties are rejected at authoring and unrepresentable in the database.</summary>
    public int Rank { get; set; }
}
