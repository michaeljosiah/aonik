using Aonik.Platform.Contracts.Models.Seeding;

namespace Aonik.Platform.Contracts.Services.Seeding;

public interface IPermissionSeedService
{
    Task<PermissionSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
