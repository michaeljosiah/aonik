using Aonik.Domain.Identity.Entities;

namespace Aonik.Application.Services.Identity;

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
