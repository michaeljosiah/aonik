using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Spec 072 Y1 — the current principal's primary Party: the latest <c>UserParty</c> link for the
/// resolved platform user (the <c>GetPrimaryPartyAsync</c> rule). Null for anonymous requests
/// and for users with no party link — callers treat null exactly like a guest.
/// </summary>
internal sealed class CurrentPartyResolver : ICurrentPartyResolver
{
    private readonly PlatformDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ITenantProvider _tenantProvider;

    public CurrentPartyResolver(
        PlatformDbContext dbContext,
        ICurrentUserProvider currentUser,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid?> GetCurrentPartyIdAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.GetCurrentUserId() is not { } userId)
        {
            return null;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await _dbContext.UserParties
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (Guid?)x.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
