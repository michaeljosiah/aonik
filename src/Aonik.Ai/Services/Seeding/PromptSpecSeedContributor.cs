using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Seeding;

internal class PromptSpecSeedContributor : IGlobalSeedContributor
{
    private readonly AiDbContext _dbContext;
    private readonly FileBasedPromptStore _fileStore;
    private readonly ILogger<PromptSpecSeedService> _logger;

    public PromptSpecSeedContributor(
        AiDbContext dbContext,
        FileBasedPromptStore fileStore,
        ILogger<PromptSpecSeedService> logger)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _logger = logger;
    }

    public string Key => "PromptSpecs";
    public string DisplayName => "Prompt Templates";
    public string Description => "Seeds AI prompt specifications from file-based templates into the database.";
    public int SortOrder => 5;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new PromptSpecSeedService(_dbContext, _fileStore, _logger);
        await service.SeedAsync(cancellationToken);
        return ["Prompt spec seed completed"];
    }
}
