using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding.Contributors;

internal class IdentitySeedContributor : IGlobalSeedContributor
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<IdentitySeedService> _logger;

    public IdentitySeedContributor(PlatformDbContext dbContext, ILogger<IdentitySeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Key => "Identity";
    public string DisplayName => "Global Permissions";
    public string Description => "Seeds global permission definitions used by the RBAC system.";
    public int SortOrder => 1;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new IdentitySeedService(_dbContext, _logger);
        await service.SeedAsync(cancellationToken);
        return ["Identity seed completed"];
    }
}
