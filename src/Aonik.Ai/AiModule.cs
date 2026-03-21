using Aonik.Ai.Middleware;
using Aonik.Ai.Persistence;
using Aonik.Ai.Providers;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // IChatClient — registered directly with AuditMiddleware in the pipeline.
        // Provider is selected via AI:Provider config key (Stub | OpenAI | AzureOpenAI).
        services.AddScoped<IChatClient>(sp =>
        {
            var provider = configuration["AI:Provider"] ?? "Stub";
            var logger = sp.GetRequiredService<ILogger<AiModule>>();
            logger.LogInformation("Creating IChatClient for provider: {Provider}", provider);

            IChatClient innerClient = provider.ToLowerInvariant() switch
            {
                "stub" => new StubChatClient(),

                "openai" => throw new NotSupportedException(
                    "OpenAI provider is not yet implemented. " +
                    "Add the Microsoft.Extensions.AI.OpenAI package and configure AI:OpenAI:ApiKey and AI:OpenAI:Model."),

                "azureopenai" or "azure_openai" or "azure-openai" => throw new NotSupportedException(
                    "Azure OpenAI provider is not yet implemented. " +
                    "Add the Azure.AI.OpenAI package and configure AI:AzureOpenAI:Endpoint, AI:AzureOpenAI:ApiKey, and AI:AzureOpenAI:DeploymentName."),

                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{provider}'. Supported values: Stub, OpenAI, AzureOpenAI.")
            };

            // Build the middleware pipeline: innerClient -> AuditMiddleware
            return innerClient
                .AsBuilder()
                .Use((inner, _) => new AuditMiddleware(
                    inner,
                    sp.GetRequiredService<IAiRunWriter>(),
                    sp.GetRequiredService<ILogger<AuditMiddleware>>()))
                .Build();
        });

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
