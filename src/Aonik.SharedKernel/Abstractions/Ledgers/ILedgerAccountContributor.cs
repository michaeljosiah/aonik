namespace Aonik.SharedKernel.Abstractions.Ledgers;

/// <summary>
/// Declares the ledger accounts a module needs, so Finance can create them at tenant provisioning
/// (Spec 088 §5, discovered by Spec 087 P5). Module-contributed via <c>IEnumerable&lt;T&gt;</c> DI.
///
/// <see cref="IJournalWriter"/> deliberately <b>rejects</b> an unknown account code rather than
/// creating one — a typo must not mint an account and quietly divert money into it. That leaves a
/// module needing somewhere legitimate to say which accounts it posts to, which is this. The chart
/// of accounts stays owned by Finance; a module only declares its requirements.
/// </summary>
public interface ILedgerAccountContributor
{
    /// <summary>Module name, for provisioning diagnostics.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Accounts this module posts to. Applied idempotently: an existing code is left exactly as it
    /// is, never renamed or retyped, because an operator may have adjusted it deliberately.
    /// </summary>
    IReadOnlyCollection<LedgerAccountDefinition> GetAccounts();
}

/// <summary>One account a module requires.</summary>
/// <param name="Code">Unique within the tenant's ledger.</param>
/// <param name="AccountType">
/// <c>Asset</c>, <c>Liability</c>, <c>Revenue</c>, <c>Expense</c>, <c>Equity</c>. Getting this
/// wrong misstates the balance sheet rather than merely mislabelling a row — prepaid units are a
/// liability until consumed, not revenue on receipt.
/// </param>
public sealed record LedgerAccountDefinition(string Code, string Name, string AccountType);
