using Aonik.Platform.Contracts.Models.Seeding;

namespace Aonik.Platform.Contracts.Services.Seeding;

public interface IDemoSeedService
{
    Task<DemoSeedResult> SeedAsync(Guid tenantId, string? seedType = null, CancellationToken cancellationToken = default);
}
