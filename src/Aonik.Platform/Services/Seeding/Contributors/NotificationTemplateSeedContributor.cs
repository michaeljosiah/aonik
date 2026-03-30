using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding.Contributors;

internal class NotificationTemplateSeedContributor : IGlobalSeedContributor
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<NotificationTemplateSeedService> _logger;

    public NotificationTemplateSeedContributor(PlatformDbContext dbContext, ILogger<NotificationTemplateSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Key => "NotificationTemplates";
    public string DisplayName => "Notification Templates";
    public string Description => "Seeds shared notification templates for email and SMS.";
    public int SortOrder => 4;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var service = new NotificationTemplateSeedService(_dbContext, _logger);
        await service.SeedAsync(cancellationToken);
        return ["Notification template seed completed"];
    }
}
