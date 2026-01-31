using Aonik.Application.Models.Customers;
using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Customers;

public interface ICustomerAdminService
{
    Task<PagedResult<CustomerListItem>> ListCustomersAsync(
        ListCustomersRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}
