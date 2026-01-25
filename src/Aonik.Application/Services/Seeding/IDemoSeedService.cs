using Aonik.Application.Models.Seeding;

namespace Aonik.Application.Services.Seeding;

public interface IDemoSeedService
{
    Task<DemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
