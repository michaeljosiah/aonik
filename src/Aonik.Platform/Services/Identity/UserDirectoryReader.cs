using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;
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
}
