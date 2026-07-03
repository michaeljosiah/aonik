namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Order party-role code constants, mirrored from
/// <c>Aonik.Finance.Entities.Orders.OrderPartyRoles</c> so cross-module consumers
/// (notably PersonalFinance) can interpret the <see cref="OrderPartyRoleItem.Role"/>
/// string without taking a dependency on Finance entities.
///
/// The Finance-internal type remains the source of truth — these constants must
/// be kept in lockstep with it. Adding a new role requires updating both.
/// </summary>
public static class OrderPartyRoleCodes
{
    /// <summary>The party initiating the order and providing funds.</summary>
    public const string Payer = "Payer";

    /// <summary>The party originating or sending funds/assets.</summary>
    public const string Sender = "Sender";

    /// <summary>The party receiving funds/assets or benefits.</summary>
    public const string Receiver = "Receiver";

    /// <summary>The party to whom payment is being made.</summary>
    public const string Payee = "Payee";

    /// <summary>The counterparty we buy from on a purchase order (Spec 053 §11) — the payee of
    /// the outward money. Written when the Commerce <c>Supplier</c> is soft-linked to a Party.</summary>
    public const string Supplier = "Supplier";
}
