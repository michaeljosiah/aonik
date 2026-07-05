using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// PlatformDbContext-backed implementation of <see cref="IUserDirectoryReader"/>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class UserDirectoryReader : IUserDirectoryReader
{
    private readonly PlatformDbContext _dbContext;

    public UserDirectoryReader(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && userIds.Contains(u.Id))
            .Select(u => new UserDirectoryItem(u.Id, u.Email, u.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDirectoryKey>> GetAllUserKeysAsync(
        CancellationToken cancellationToken = default)
    {
        // Deliberately cross-tenant (Spec 027 S5, #126): the PersonalFinance
        // profile seed ensures EVERY platform user has a PersonalProfile, so it
        // must span tenants regardless of the ambient tenant context. User is
        // ITenantScoped, so AonikDbContextBase applies a tenant query filter;
        // AcrossTenants() is the sanctioned escape hatch (bare IgnoreQueryFilters
        // is banned by BannedSymbols.txt).
        return await _dbContext.Users
            .AsNoTracking()
            .AcrossTenants()
            .Select(u => new UserDirectoryKey(u.Id, u.TenantId))
            .ToListAsync(cancellationToken);
    }
}
