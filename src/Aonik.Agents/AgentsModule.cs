using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents;

/// <summary>
/// Agents module registration. Owns Agent framework entities
/// (Agent, AgentRun, OrchestratorPolicy, Proposal) and the
/// domain agent infrastructure (AonikDomainAgent base, middleware).
/// </summary>
public sealed class AgentsModule : IModule
{
    public static string Name => "Agents";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register AgentsDbContext
        // Shares the same physical database as AonikDbContext, PlatformDbContext,
        // FinanceDbContext, and AiDbContext.
        // Uses the 'agents' schema for logical isolation.
        services.AddDbContext<AgentsDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"AgentsDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString);
            }
        });

        // Future: register agent services, proposal services, orchestrator services
        // These will be added in PR 3.3+ as domain agents are implemented.

        return services;
    }
}

/// <summary>
/// Extension methods for registering the Agents module in the DI container.
/// </summary>
public static class AgentsModuleExtensions
{
    /// <summary>
    /// Adds the Agents module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddAgentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => AgentsModule.ConfigureServices(services, configuration);
}
