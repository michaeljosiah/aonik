namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Canonical party-role codes shared across modules. A role answers "who this party is to us"
/// and is stored on <c>PartyRoleAssignment.Role</c>. These live in SharedKernel so that consumer
/// modules (e.g. Finance) can reference the exact string the Platform module persists and queries,
/// without taking a cross-module reference on Platform's entity constants.
/// </summary>
public static class PartyRoleCodes
{
    /// <summary>A party that accepts payments for goods or services.</summary>
    public const string Merchant = "Merchant";

    /// <summary>A party that purchases goods or services.</summary>
    public const string Customer = "Customer";

    /// <summary>A party that receives funds or benefits from a transaction (a payable destination).</summary>
    public const string Beneficiary = "Beneficiary";
}

/// <summary>
/// Canonical party-relationship type codes shared across modules. A relationship type describes the
/// directed edge between two parties (<c>PartyRelationship.RelationshipTypeCode</c>). Kinship codes
/// (Mother, Spouse, Sibling, …) stay defined in the Platform module; this class only exposes the
/// neutral, non-kin codes a consumer module needs to create an edge through <c>IPartyService</c>.
/// </summary>
public static class PartyRelationshipTypeCodes
{
    /// <summary>
    /// A neutral payee relationship: the customer pays or sends money to this party, but the party
    /// is not a relative. Pairs with the <see cref="PartyRoleCodes.Beneficiary"/> role to mark a
    /// saved payout destination as "payable" without overloading the kinship vocabulary.
    /// </summary>
    public const string Recipient = "Recipient";
}
