using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Promotions;

/// <summary>
/// A coupon / discount that reduces a cart's payable amount at checkout (Spec 042 §5 follow-up).
/// Either a percentage of the subtotal or a fixed amount in a currency. Anemic.
/// </summary>
public class Discount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>Percentage | FixedAmount. See <see cref="DiscountKinds"/>.</summary>
    public string Kind { get; set; } = DiscountKinds.Percentage;

    /// <summary>A percentage (0–100) for Percentage, or a money amount for FixedAmount.</summary>
    public decimal Value { get; set; }

    /// <summary>Required for a FixedAmount discount; the discount only applies to carts in this currency.</summary>
    public string? Currency { get; set; }

    public bool IsActive { get; set; } = true;
    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Known values for <see cref="Discount.Kind"/>.</summary>
public static class DiscountKinds
{
    public const string Percentage = "Percentage";
    public const string FixedAmount = "FixedAmount";
}
