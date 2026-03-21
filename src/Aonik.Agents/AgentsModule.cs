using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents;

/// <summary>
/// Agents module registration. Owns Agent framework entities
/// (Agent, AgentRun, OrchestratorPolicy, Proposal) and the
/// domain agent infrastructure (IDomainAgentDescriptor, workflows, MCP).
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
        // FinanceDbContext, and AiDbContext using dbo schema + module table prefixes.
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
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString);
            }
        });

        // Agent configuration service — manages persisted agent configs with
        // two-level override model (global defaults + tenant overrides).
        services.AddScoped<IAgentConfigurationService, AgentConfigurationService>();

        // Seed global default agent configurations on startup
        services.AddHostedService<AgentConfigurationSeedingService>();

        // MCP tool provider — connects to MCP servers and exposes their tools as AITool
        // instances for use by agents. Registered as singleton since it manages long-lived
        // stdio connections to MCP server processes.
        services.AddSingleton<IMcpToolProvider, McpToolProvider>();

        // Master orchestrator — routes user messages to domain agents via agent-as-tool pattern.
        // Scoped because it depends on IChatClient (scoped from AiModule).
        services.AddScoped<IMasterOrchestratorService, MasterOrchestratorService>();

        // Workflow factories — keyed by workflow name (R10).
        // RunWorkflowEndpoint resolves the factory via GetKeyedService<IWorkflowFactory>(name).
        services.AddKeyedSingleton<IWorkflowFactory, InvoiceProcessingWorkflowFactory>(
            InvoiceProcessingWorkflowFactory.Name);
        services.AddKeyedSingleton<IWorkflowFactory, OnboardingWorkflowFactory>(
            OnboardingWorkflowFactory.Name);
        services.AddKeyedSingleton<IWorkflowFactory, ReconciliationWorkflowFactory>(
            ReconciliationWorkflowFactory.Name);

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
