using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IUserProvisioningService
{
    Task<UserProvisioningResult> EnsureUserAndCustomerAsync(
        IExternalIdentity identity,
        CancellationToken cancellationToken = default);
}
