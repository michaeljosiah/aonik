using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IBootstrapService
{
    Task<BootstrapTenantResult> BootstrapAsync(
        BootstrapOwnerContext ownerContext,
        CancellationToken cancellationToken = default);
}
