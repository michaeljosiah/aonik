using Aonik.Application.Models.Seeding;

namespace Aonik.Application.Services.Seeding;

public interface IPermissionSeedService
{
    Task<PermissionSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
