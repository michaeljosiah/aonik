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

    public async Task<IReadOnlyList<UserDirectoryKey>> GetAllUserKeysAsync(
        CancellationToken cancellationToken = default)
    {
        // Plain read of the Users read model with the context's ambient tenant
        // filter applied — a direct, behaviour-preserving port of the prior
        // PersonalFinanceSeedContributor read of FinanceDbContext.Users
        // (Spec 027 S5, #126). It deliberately does NOT add a cross-tenant
        // bypass the original read lacked; a genuinely global profile seed would
        // be a separate, deliberate change.
        return await _dbContext.Users
            .AsNoTracking()
            .Select(u => new UserDirectoryKey(u.Id, u.TenantId))
            .ToListAsync(cancellationToken);
    }
}
