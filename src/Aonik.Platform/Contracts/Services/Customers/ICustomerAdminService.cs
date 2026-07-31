using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Customers;

public interface ICustomerAdminService
{
    /// <summary>
    /// Spec 080 — the product lines that actually have customers in this tenant, so the registry
    /// only offers filter tabs that can return rows. Aggregated from the registered
    /// <c>ICustomerRegistryContributor</c>s; a module that is not installed contributes nothing.
    /// </summary>
    Task<CustomerRegistryDomainsResponse> GetRegistryDomainsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<CustomerListItem>> ListCustomersAsync(
        ListCustomersRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<CustomerStats?> GetCustomerStatsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent activity entries for the customer, merged
    /// across finance events (via ICustomerActivityProvider), audit logs
    /// scoped to the party, and document uploads. Returns null when the
    /// party doesn't exist; otherwise an empty list when there is no
    /// activity yet.
    /// </summary>
    Task<IReadOnlyList<CustomerActivityEntryDto>?> GetCustomerActivityAsync(
        Guid partyId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<CreateCustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);
}
