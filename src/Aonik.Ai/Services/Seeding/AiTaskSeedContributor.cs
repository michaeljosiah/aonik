using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Seeding;

internal class AiTaskSeedContributor : IGlobalSeedContributor
{
    private readonly AiDbContext _dbContext;
    private readonly ILogger<AiTaskSeedService> _logger;

    public AiTaskSeedContributor(
        AiDbContext dbContext,
        ILogger<AiTaskSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Key => "AiTasks";
    public string DisplayName => "AI Tasks";
    public string Description => "Seeds AI task definitions with embedded prompt templates.";
    public int SortOrder => 5;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new AiTaskSeedService(_dbContext, _logger);
        await service.SeedAsync(cancellationToken);
        return ["AI task seed completed"];
    }
}
