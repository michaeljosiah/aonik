using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

/// <summary>
/// A signed, single-use, expiring invite to join an owner's circle (Spec 048 §7).
/// Carries the grant terms; on accept it materialises a <see cref="CircleGrant"/>.
/// The link rides the OS share sheet (WhatsApp-first); email/phone are delivery hints.
/// </summary>
public class CircleInvite : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OwnerUserId { get; set; }

    /// <summary>Opaque, cryptographically random, unique per tenant.</summary>
    public string Token { get; set; } = string.Empty;

    public string Scope { get; set; } = "entities";
    public string EntityIdsJson { get; set; } = "[]";
    public bool NoAmounts { get; set; }

    /// <summary>email | phone | link — optional delivery hint.</summary>
    public string? Channel { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>pending | accepted | expired | revoked.</summary>
    public string Status { get; set; } = "pending";

    public DateTime? ConsumedAt { get; set; }

    /// <summary>The grant created when this invite was accepted.</summary>
    public Guid? GrantId { get; set; }
}
