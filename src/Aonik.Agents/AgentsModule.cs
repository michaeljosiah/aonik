using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows;
using Aonik.Agents.Workflows.Graph;
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
                options.UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.EnableRetryOnFailure());
            }
        });

        // Agent configuration service — manages persisted agent configs with
        // two-level override model (global defaults + tenant overrides).
        services.AddScoped<IAgentConfigurationService, AgentConfigurationService>();

        // Agent run service — queries agent execution history.
        services.AddScoped<IAgentRunService, AgentRunService>();

        // Workflow registry — list / get / runs / versions.
        services.AddScoped<IWorkflowService, Services.Workflows.WorkflowService>();

        // Demo seed contributor — owns the Workflows phase (seven domain
        // agents + seven workflow registry rows with full graphs).
        services.AddScoped<Aonik.SharedKernel.Abstractions.IDemoSeedContributor, Services.Seeding.AgentsDemoSeedContributor>();

        // Cross-module read aggregate consumed by Finance dashboards (e.g. MySpace).
        services.AddScoped<Aonik.SharedKernel.Abstractions.Agents.IAgentProposalQueryService, Services.Insights.AgentProposalQueryService>();

        // Proposal approval pipeline — wires the dashboard's Apply / Dismiss / Review actions.
        services.AddScoped<IProposalApprovalService, Services.ProposalApprovalService>();

        // Seed global default agent configurations on startup
        services.AddHostedService<AgentConfigurationSeedingService>();

        // MCP tool provider — connects to MCP servers and exposes their tools as AITool
        // instances for use by agents. Registered as singleton since it manages long-lived
        // stdio connections to MCP server processes.
        services.AddSingleton<IMcpToolProvider, McpToolProvider>();

        // Master orchestrator — routes user messages to domain agents via agent-as-tool pattern.
        // Scoped because it depends on IChatClient (scoped from AiModule).
        services.AddScoped<IMasterOrchestratorService, MasterOrchestratorService>();

        // Domain agent resolver — memoises built domain agents within a request scope.
        // Safe per-scope because the agent captures IChatClient + tool scoped services;
        // NOT safe to hoist to singleton for that reason.
        services.AddScoped<IDomainAgentResolver, DomainAgentResolver>();

        // Playground scenario service — manages saved test conversation setups.
        services.AddScoped<IPlaygroundScenarioService, PlaygroundScenarioService>();

        // Chat thread persistence — manages persisted threads and messages.
        // Scoped because it depends on AgentsDbContext and tenant/user providers.
        services.AddScoped<IChatThreadService, ChatThreadService>();

        // Thread title generator — uses IChatClient to summarise the first user message.
        // Scoped because it depends on IChatClient (scoped from AiModule).
        services.AddScoped<IChatThreadTitleGenerator, ChatThreadTitleGenerator>();

        // User brief projector — assembles the compact user context payload for agent sessions.
        services.AddScoped<IUserBriefProjector, Services.UserBriefProjector>();

        // Tool call classifier — centralises tool-name conventions used by the streaming endpoints.
        services.AddScoped<IToolCallClassifier, Services.ToolCallClassifier>();

        // AG-UI ↔ M.E.AI message/tool converter.
        services.AddScoped<IAguiMessageConverter, Services.AguiMessageConverter>();

        // Pre-flight voice-mode validator — runs all TTS / format / tenant
        // checks before any SSE bytes go on the wire so failures surface
        // as plain JSON 400 rather than half-streamed SSE.
        services.AddScoped<IAguiVoiceModeValidator, Services.AguiVoiceModeValidator>();

        // Run-options builder — combines client tool declarations with the
        // agent's per-config model override into ChatClientAgentRunOptions.
        services.AddSingleton<IAguiRunOptionsBuilder, Services.AguiRunOptionsBuilder>();

        // SSE protocol pipeline — owns the AG-UI translation of the
        // agent's framework streaming output. Keeps the endpoint thin.
        services.AddScoped<IAguiStreamPipeline, Services.AguiStreamPipeline>();

        // Chat thread manager — thread load/create + fire-and-forget user-message
        // append + thin-client history reconstruction.
        services.AddScoped<IChatThreadManager, Services.ChatThreadManager>();

        // Recent AG-UI thread history cache — used to avoid reconstructing
        // thin-client turns from persistent storage on every request.
        services.AddSingleton<IChatThreadHistoryCache, Services.ChatThreadHistoryCache>();

        // Agent contextualizer — resolves the target agent and projects the user
        // brief preamble when the agent's descriptor requires it.
        services.AddScoped<IAgentContextualizer, Services.AgentContextualizer>();

        // Post-stream persistence coordinator — runs thread message persistence,
        // title generation, and AiRun metrics writes after the response flushes.
        services.AddScoped<IPostStreamPersistenceCoordinator, Services.PostStreamPersistenceCoordinator>();


        // Conversation summary generator — produces session summaries from chat threads.
        services.AddScoped<Services.ConversationSummaryGenerator>();
        services.AddScoped<IConversationSummaryService>(sp =>
            sp.GetRequiredService<Services.ConversationSummaryGenerator>());

        // Workflow factories — keyed by workflow name (R10).
        // RunWorkflowEndpoint resolves the factory via GetKeyedService<IWorkflowFactory>(name).
        services.AddKeyedSingleton<IWorkflowFactory, InvoiceProcessingWorkflowFactory>(
            InvoiceProcessingWorkflowFactory.Name);
        services.AddKeyedSingleton<IWorkflowFactory, OnboardingWorkflowFactory>(
            OnboardingWorkflowFactory.Name);
        services.AddKeyedSingleton<IWorkflowFactory, ReconciliationWorkflowFactory>(
            ReconciliationWorkflowFactory.Name);

        // Generic graph-driven runner — translates the editor's saved
        // Workflow + WorkflowNode + WorkflowEdge rows into a MAF Workflow
        // and runs it directly via InProcessExecution. RunWorkflowEndpoint
        // falls through to this when no keyed legacy factory matches the
        // requested slug.
        services.AddScoped<IGraphWorkflowRunner, GraphWorkflowRunner>();

        // NOTE: RagContextProvider registration is deferred to the composition root (Program.cs)
        // where both Infrastructure and Agents modules are registered, avoiding circular dependencies.
        // See Program.cs for its registration alongside the adapter factories.

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
