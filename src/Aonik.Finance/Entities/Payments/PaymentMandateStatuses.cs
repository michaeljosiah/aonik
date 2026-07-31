namespace Aonik.Finance.Entities.Payments;

/// <summary>Lifecycle of a <see cref="PaymentMandate"/> (Spec 088 §6).</summary>
public static class PaymentMandateStatuses
{
    /// <summary>Chargeable.</summary>
    public const string Active = "active";

    /// <summary>Withdrawn by the customer, or by the provider on their behalf.</summary>
    public const string Revoked = "revoked";

    /// <summary>Lapsed — a replaced card, a provider mandate that timed out. Treated exactly as revoked.</summary>
    public const string Expired = "expired";
}
