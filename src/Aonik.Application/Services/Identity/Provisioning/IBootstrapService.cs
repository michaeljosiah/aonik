using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity.Provisioning;

public interface IBootstrapService
{
    Task<BootstrapTenantResult> BootstrapAsync(
        BootstrapUserContext userContext,
        CancellationToken cancellationToken = default);
}
