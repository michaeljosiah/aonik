using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IUserProvisioningService
{
    Task<UserProvisioningResult> EnsureUserAndCustomerAsync(
        IExternalIdentity identity,
        CancellationToken cancellationToken = default);
}
