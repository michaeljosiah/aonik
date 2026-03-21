using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Seeds global default agent configuration rows on startup.
/// Creates a scope to resolve scoped services (DbContext, TenantProvider).
/// Idempotent — only inserts rows that don't already exist.
/// </summary>
internal sealed class AgentConfigurationSeedingService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentConfigurationSeedingService> _logger;

    public AgentConfigurationSeedingService(
        IServiceProvider serviceProvider,
        ILogger<AgentConfigurationSeedingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IAgentConfigurationService>();
            await configService.SeedGlobalDefaultsAsync(scope.ServiceProvider, cancellationToken);

            _logger.LogInformation("Agent configuration global defaults seeded successfully");
        }
        catch (Exception ex)
        {
            // Don't crash the app if seeding fails — the system still works with
            // code-based descriptors as fallback
            _logger.LogWarning(
                ex,
                "Failed to seed agent configuration defaults — agents will use code-based descriptors");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
