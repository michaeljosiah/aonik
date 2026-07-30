namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Reports which customers a module counts as its own, so the unified Customers registry
/// (Spec 080) can show domain chips and filter by domain without Platform knowing any module's
/// tables. One implementation per product line; Platform aggregates whatever is registered, so a
/// module that is not installed simply contributes nothing rather than being special-cased.
/// </summary>
/// <remarks>
/// Deliberately batch-shaped. The registry renders a page of customers at a time, and the domain
/// filter has to be applied BEFORE pagination — a per-party call would be N+1 on every page, and
/// filtering the loaded page client-side would page wrongly (Spec 080 §2).
/// </remarks>
public interface ICustomerRegistryContributor
{
    /// <summary>
    /// Stable key for this product line, e.g. <c>storefront</c>. Serialized to the client as a
    /// domain chip and accepted as the <c>domain=</c> filter value, so it is a contract: renaming
    /// one breaks saved views. See <see cref="CustomerRegistryDomains"/> for the known keys.
    /// </summary>
    string DomainKey { get; }

    /// <summary>
    /// The subset of <paramref name="partyIds"/> this module counts as participants, scoped to the
    /// current tenant. Pass null or an empty collection to ask for EVERY participant — that is the
    /// form the <c>domain=</c> filter uses, because narrowing the registry query needs the whole
    /// set before paging.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetParticipantsAsync(
        IReadOnlyCollection<Guid>? partyIds,
        CancellationToken cancellationToken = default);

    // Deliberately NO "does this domain have anyone" shortcut: a module's ownership records do
    // not all belong to registry customers, so only Platform — which owns the registry
    // predicate — can decide whether a domain would actually return rows.
}

/// <summary>The domain keys shipped today. Open by design — a new product line adds its own
/// contributor and key without touching Platform.</summary>
public static class CustomerRegistryDomains
{
    /// <summary>Finance — the party holds a customer account that can be invoiced.</summary>
    public const string Billing = "billing";

    /// <summary>Commerce — the party has a storefront cart (and therefore any order it produced).</summary>
    public const string Storefront = "storefront";

    /// <summary>PersonalFinance — the party is enrolled with a personal profile.</summary>
    public const string PersonalFinance = "personal-finance";
}
