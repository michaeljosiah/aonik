using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Persistence;
using Aonik.Platform.Entities.Identity;

namespace Aonik.Platform.Services.Seeding.Phases;

/// <summary>
/// Ensures the TenantAdmin role exists and is assigned to the current user.
/// Called at Phase 4 of the demo seed pipeline.
/// </summary>
internal sealed class IdentityRoleSeedPhase
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public IdentityRoleSeedPhase(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task EnsureTenantAdminRoleAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var tenantAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.TenantId == tenantId && role.Name == "TenantAdmin", cancellationToken);

        if (tenantAdminRole == null)
        {
            tenantAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "TenantAdmin",
                CreatedAt = _clock.UtcNow,
                CreatedBy = userId
            };
            _dbContext.Roles.Add(tenantAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
            operations.Add("Created TenantAdmin role");
        }

        var hasRole = await _dbContext.UserRoles
            .AnyAsync(link => link.UserId == userId && link.RoleId == tenantAdminRole.Id, cancellationToken);

        if (!hasRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId.Value,
                RoleId = tenantAdminRole.Id,
                CreatedAt = _clock.UtcNow,
                CreatedBy = userId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            operations.Add("Assigned TenantAdmin role");
        }
    }
}
