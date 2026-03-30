using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding.Contributors;

internal class SettingsSeedContributor : IGlobalSeedContributor
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<SettingsSeedService> _logger;

    public SettingsSeedContributor(PlatformDbContext dbContext, ILogger<SettingsSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Key => "Settings";
    public string DisplayName => "Global Settings";
    public string Description => "Seeds default global settings from setting definitions.";
    public int SortOrder => 3;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new SettingsSeedService(_dbContext, _logger);
        await service.SeedAsync(cancellationToken);
        return ["Settings seed completed"];
    }
}
