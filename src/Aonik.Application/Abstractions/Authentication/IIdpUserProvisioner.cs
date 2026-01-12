using Aonik.Application.Models.Authentication;

namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpUserProvisioner
{
    Task<ExternalIdentityResult> CreateUserAsync(
        IdpUserRegistration registration,
        CancellationToken cancellationToken = default);
}
