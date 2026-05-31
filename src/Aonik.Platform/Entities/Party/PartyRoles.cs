using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// Defines party relationship roles that describe "who this party is to us".
/// These are business classifications used in PartyRoleAssignment.
/// Values are aliased to <see cref="PartyRoleCodes"/> in SharedKernel so the
/// strings stay identical to the ones consumer modules (e.g. Finance) pass in.
/// </summary>
public static class PartyRoles
{
    /// <summary>
    /// A party that accepts payments for goods or services
    /// </summary>
    public const string Merchant = PartyRoleCodes.Merchant;

    /// <summary>
    /// A party that purchases goods or services
    /// </summary>
    public const string Customer = PartyRoleCodes.Customer;

    /// <summary>
    /// A party that receives funds or benefits from a transaction
    /// </summary>
    public const string Beneficiary = PartyRoleCodes.Beneficiary;
}
