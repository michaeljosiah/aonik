using Aonik.Platform.Contracts.Models.Authentication;

namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpUserProvisioner
{
    Task<ExternalIdentityResult> CreateUserAsync(
        IdpUserRegistration registration,
        CancellationToken cancellationToken = default);
}
