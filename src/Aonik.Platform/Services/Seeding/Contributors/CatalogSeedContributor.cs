using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding.Contributors;

internal class CatalogSeedContributor : IGlobalSeedContributor
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<CatalogSeedService> _logger;

    public CatalogSeedContributor(PlatformDbContext dbContext, ILogger<CatalogSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Key => "Catalog";
    public string DisplayName => "Reference Data Catalog";
    public string Description => "Seeds countries, currencies, customer tiers, and other reference data.";
    public int SortOrder => 2;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new CatalogSeedService(_dbContext, _logger);
        await service.SeedAsync(cancellationToken);
        return ["Catalog seed completed"];
    }
}
