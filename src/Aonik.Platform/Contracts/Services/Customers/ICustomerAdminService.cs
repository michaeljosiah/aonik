using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Customers;

public interface ICustomerAdminService
{
    Task<PagedResult<CustomerListItem>> ListCustomersAsync(
        ListCustomersRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<CustomerStats?> GetCustomerStatsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<CreateCustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);
}
