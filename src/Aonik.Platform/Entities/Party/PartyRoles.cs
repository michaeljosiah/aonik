namespace Aonik.Platform.Entities.Party;

/// <summary>
/// Defines party relationship roles that describe "who this party is to us".
/// These are business classifications used in PartyRoleAssignment.
/// </summary>
public static class PartyRoles
{
    /// <summary>
    /// A party that accepts payments for goods or services
    /// </summary>
    public const string Merchant = "Merchant";

    /// <summary>
    /// A party that purchases goods or services
    /// </summary>
    public const string Customer = "Customer";

    /// <summary>
    /// A party that receives funds or benefits from a transaction
    /// </summary>
    public const string Beneficiary = "Beneficiary";
}
