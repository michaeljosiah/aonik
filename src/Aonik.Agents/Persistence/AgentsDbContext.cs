using Aonik.Agents.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Persistence;

/// <summary>
/// Module-scoped DbContext for the Agents domain.
/// Owns: Agent, AgentRun, OrchestratorPolicy, Proposal.
/// Shares the same physical database as AonikDbContext but uses the 'agents' schema
/// for logical isolation.
/// </summary>
internal class AgentsDbContext : AonikDbContextBase
{
    public DbSet<Agent> Agents { get; set; } = null!;
    public DbSet<AgentRun> AgentRuns { get; set; } = null!;
    public DbSet<OrchestratorPolicy> OrchestratorPolicies { get; set; } = null!;
    public DbSet<Proposal> Proposals { get; set; } = null!;

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

        // Default schema for this module
        modelBuilder.HasDefaultSchema(SchemaNames.Agents);

        // All agent entities were created in dbo schema by existing migrations.
        // Map them explicitly to dbo to avoid schema mismatch.
        modelBuilder.Entity<Agent>().ToTable("Agents", SchemaNames.Default);
        modelBuilder.Entity<AgentRun>().ToTable("AgentRuns", SchemaNames.Default);
        modelBuilder.Entity<OrchestratorPolicy>().ToTable("OrchestratorPolicies", SchemaNames.Default);
        modelBuilder.Entity<Proposal>().ToTable("Proposals", SchemaNames.Default);

        // Apply tenant query filters for ITenantScoped entities (AgentRun, Proposal)
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId (Agent, OrchestratorPolicy)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(OrchestratorPolicy));
    }
}
