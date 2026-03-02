using Aonik.Ai.Persistence;
using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Ai;

/// <summary>
/// AI module registration. Owns AI Platform entities (providers, models, policies,
/// prompts, tools, runs, traces, feedback, evals, insights, signals).
/// Provides horizontal AI infrastructure consumed by domain modules.
/// </summary>
public sealed class AiModule : IModule
{
    public static string Name => "Ai";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register AiDbContext
        // Shares the same physical database as AonikDbContext, PlatformDbContext, and FinanceDbContext
        // using dbo schema + module table prefixes.
        services.AddDbContext<AiDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"AiDb_{Guid.NewGuid()}";
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

        // ── AI Infrastructure ────────────────────────────────────────
        // Prompt store (loads .md templates from disk)
        services.AddSingleton<IPromptStore>(sp =>
        {
            var promptPath = configuration["AI:PromptTemplatesPath"];
            return new FileBasedPromptStore(promptPath);
        });

        // Chat client factory — config-driven provider selection (AI:Provider = Stub | OpenAI | AzureOpenAI)
        services.AddSingleton<IChatClientFactory, ConfigDrivenChatClientFactory>();

        // IChatClient — resolved from the factory per scope
        services.AddScoped<IChatClient>(sp =>
            sp.GetRequiredService<IChatClientFactory>().CreateClient());

        // ── AI Services ──────────────────────────────────────────────
        // Insight persistence — consumed by domain modules via IInsightWriter contract
        services.AddScoped<IInsightWriter, InsightWriter>();
        services.AddScoped<IAiRunWriter, AiRunWriter>();

        // Cross-module provisioning contributor
        services.AddScoped<Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor, Services.AiTenantProvisioningContributor>();

        return services;
    }
}

/// <summary>
/// Extension methods for registering the AI module in the DI container.
/// </summary>
public static class AiModuleExtensions
{
    /// <summary>
    /// Adds the AI module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => AiModule.ConfigureServices(services, configuration);
}
