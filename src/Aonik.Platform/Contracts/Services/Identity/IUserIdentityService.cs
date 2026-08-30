using Aonik.Platform.Entities.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IUserIdentityService
{
    Task<Guid?> ResolvePendingTenantByEmailAsync(
        string? email,
        CancellationToken ct = default);

    Task<User> ResolveOrCreateUserAsync(
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        Guid aonikTenantId,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<string>> GetRoleNamesAsync(
        Guid userId,
        CancellationToken ct = default);
}
