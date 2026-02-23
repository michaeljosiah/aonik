using Aonik.Platform.Entities.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IUserIdentityService
{
    Task<User> ResolveOrCreateUserAsync(
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        Guid aonikTenantId,
        CancellationToken ct = default);
}
