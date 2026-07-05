namespace Aonik.SharedKernel.Abstractions.PersonalFinance;

/// <summary>
/// Cross-module port for tearing down PersonalFinance demo-seed data. Platform's
/// demo-seed reverse orchestrator (<c>ReverseSeedPhase</c>) invokes this instead
/// of depending on <c>PersonalFinanceDbContext</c> directly: the PF DbSets are
/// owned solely by PersonalFinance (Spec 027 S3, #126), so the teardown logic
/// lives with them. Keeps Platform free of a Platform → PersonalFinance edge.
/// </summary>
public interface IPersonalFinanceDemoDataReverser
{
    /// <summary>
    /// Deletes the year-of-PF-activity demo data (personal transactions, recurring
    /// bills, subscriptions, personal accounts, personal profiles) for the given
    /// persona user ids in the tenant. Returns the per-entity deletion counts.
    /// </summary>
    Task<PersonalFinanceDemoReversalCounts> ReversePersonaActivityAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> personaUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes demo households (and their members) whose name matches one of
    /// <paramref name="householdNames"/> in the tenant. Returns the number of
    /// households removed.
    /// </summary>
    Task<int> ReverseHouseholdsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> householdNames,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-entity deletion counts returned by
/// <see cref="IPersonalFinanceDemoDataReverser.ReversePersonaActivityAsync"/>.
/// </summary>
public sealed record PersonalFinanceDemoReversalCounts(
    int Transactions,
    int RecurringBills,
    int Subscriptions,
    int Accounts,
    int Profiles)
{
    public int Total => Transactions + RecurringBills + Subscriptions + Accounts + Profiles;
}
