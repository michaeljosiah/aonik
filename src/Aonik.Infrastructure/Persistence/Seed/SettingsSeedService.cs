using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Settings;
using Aonik.Platform.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds Global-scope setting defaults from <see cref="SettingDefinitions"/>.
/// Only inserts a row when no Global-scope row exists for the key, so
/// admin-edited values are never overwritten. Idempotent and safe to call on
/// every startup.
/// </summary>
public class SettingsSeedService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ILogger<SettingsSeedService> _logger;

    public SettingsSeedService(IAonikDbContext dbContext, ILogger<SettingsSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting settings seed process...");

        var definitions = SettingDefinitions.All;

        // Only consider definitions that have a non-null default value
        var definitionsWithDefaults = definitions
            .Where(d => d.DefaultValue != null)
            .ToList();

        if (definitionsWithDefaults.Count == 0)
        {
            _logger.LogInformation("No setting definitions with defaults found - skipping seed");
            return;
        }

        var existingKeys = await _dbContext.Settings
            .Where(s => s.Scope == SettingScope.Global && s.TenantId == null && s.UserId == null)
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var existingKeySet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);

        var newSettings = definitionsWithDefaults
            .Where(d => !existingKeySet.Contains(d.Key))
            .Select(d => new Setting
            {
                Key = d.Key,
                Value = d.DefaultValue,
                Scope = SettingScope.Global,
                TenantId = null,
                UserId = null
            })
            .ToList();

        if (newSettings.Count > 0)
        {
            await _dbContext.Settings.AddRangeAsync(newSettings, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} new global settings", newSettings.Count);
        }
        else
        {
            _logger.LogInformation("All global settings already exist - skipping seed");
        }
    }
}
