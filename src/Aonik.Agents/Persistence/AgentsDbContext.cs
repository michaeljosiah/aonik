using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Persistence;

/// <summary>
/// Module-scoped DbContext for the Agents domain.
/// Owns: Agent, AgentRun, OrchestratorPolicy, Proposal, ChatThread, ChatThreadMessage.
/// Shares the same physical database as AonikDbContext using dbo schema
/// with module table prefixes for logical isolation.
/// </summary>
internal class AgentsDbContext : AonikDbContextBase
{
    public DbSet<Agent> Agents { get; set; } = null!;
    public DbSet<AgentRun> AgentRuns { get; set; } = null!;
    public DbSet<OrchestratorPolicy> OrchestratorPolicies { get; set; } = null!;
    public DbSet<Proposal> Proposals { get; set; } = null!;
    public DbSet<ChatThread> ChatThreads { get; set; } = null!;
    public DbSet<ChatThreadMessage> ChatThreadMessages { get; set; } = null!;
    public DbSet<ConversationSummary> ConversationSummaries { get; set; } = null!;
    public DbSet<PlaygroundScenario> PlaygroundScenarios { get; set; } = null!;
    public DbSet<PlaygroundScenarioTurn> PlaygroundScenarioTurns { get; set; } = null!;

    // Workflows
    public DbSet<Workflow> Workflows { get; set; } = null!;
    public DbSet<WorkflowNode> WorkflowNodes { get; set; } = null!;
    public DbSet<WorkflowEdge> WorkflowEdges { get; set; } = null!;
    public DbSet<WorkflowVersion> WorkflowVersions { get; set; } = null!;
    public DbSet<WorkflowComment> WorkflowComments { get; set; } = null!;
    public DbSet<WorkflowRun> WorkflowRuns { get; set; } = null!;

    public AgentsDbContext(
        DbContextOptions<AgentsDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        // Apply entity configurations from this assembly (ChatThreadConfiguration, etc.)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentsDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        // Configure RowVersion as optimistic concurrency token on all AuditableEntity types
        ConfigureRowVersions(modelBuilder);

        // Apply tenant query filters for ITenantScoped entities (AgentRun, Proposal, ChatThread, ChatThreadMessage)
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId (Agent, OrchestratorPolicy)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(OrchestratorPolicy));
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Agent>(modelBuilder, "Agents");
        MapTable<AgentRun>(modelBuilder, "AgentRuns");
        MapTable<OrchestratorPolicy>(modelBuilder, "OrchestratorPolicies");
        MapTable<Proposal>(modelBuilder, "Proposals");
        MapTable<ChatThread>(modelBuilder, "ChatThreads");
        MapTable<ChatThreadMessage>(modelBuilder, "ChatThreadMessages");

        MapTable<PlaygroundScenario>(modelBuilder, "PlaygroundScenarios");
        MapTable<PlaygroundScenarioTurn>(modelBuilder, "PlaygroundScenarioTurns");

        // Workflows
        MapTable<Workflow>(modelBuilder, "Workflows");
        MapTable<WorkflowNode>(modelBuilder, "WorkflowNodes");
        MapTable<WorkflowEdge>(modelBuilder, "WorkflowEdges");
        MapTable<WorkflowVersion>(modelBuilder, "WorkflowVersions");
        MapTable<WorkflowComment>(modelBuilder, "WorkflowComments");
        MapTable<WorkflowRun>(modelBuilder, "WorkflowRuns");

        // ConversationSummary is already owned by the canonical AonikDbContext
        // migration stream as dbo.ConversationSummaries.
        modelBuilder.Entity<ConversationSummary>()
            .ToTable("ConversationSummaries", SchemaNames.Default);
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Agents}{tableName}", SchemaNames.Default);
    }
}
