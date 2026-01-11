using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IUserProfileService
{
    Task<CurrentUserSnapshot?> GetCurrentUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfile?> UpdateCustomerProfileAsync(
        Guid userId,
        Guid tenantId,
        CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken = default);
}
