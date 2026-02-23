using Aonik.Ai.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Persistence;

/// <summary>
/// Module-scoped DbContext for the AI platform domain.
/// Owns AiProvider, AiModel, AiRoutePolicy, PromptSpec, ToolSpec, AiPolicy,
/// AiRun, AiTrace, AiFeedback, EvalSuite, EvalRun, Insight, Signal entities.
/// Inherits multi-tenancy enforcement and audit stamping from <see cref="AonikDbContextBase"/>.
///
/// During migration, entities are progressively moved here from AonikDbContext.
/// Both contexts share the same physical SQL Server database.
/// </summary>
internal class AiDbContext : AonikDbContextBase
{
    // ── Providers & Models ─────────────────────────────────────────
    public DbSet<AiProvider> AiProviders { get; set; } = null!;
    public DbSet<AiModel> AiModels { get; set; } = null!;
    public DbSet<AiRoutePolicy> AiRoutePolicies { get; set; } = null!;

    // ── Prompts & Tools ────────────────────────────────────────────
    public DbSet<PromptSpec> PromptSpecs { get; set; } = null!;
    public DbSet<ToolSpec> ToolSpecs { get; set; } = null!;

    // ── Policy & Execution ─────────────────────────────────────────
    public DbSet<AiPolicy> AiPolicies { get; set; } = null!;
    public DbSet<AiRun> AiRuns { get; set; } = null!;
    public DbSet<AiTrace> AiTraces { get; set; } = null!;
    public DbSet<AiFeedback> AiFeedbacks { get; set; } = null!;

    // ── Evaluation ─────────────────────────────────────────────────
    public DbSet<EvalSuite> EvalSuites { get; set; } = null!;
    public DbSet<EvalRun> EvalRuns { get; set; } = null!;

    // ── Insights & Signals ─────────────────────────────────────────
    public DbSet<Insight> Insights { get; set; } = null!;
    public DbSet<Signal> Signals { get; set; } = null!;

    public AiDbContext(
        DbContextOptions<AiDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All AI entities use the 'ai' schema by default
        modelBuilder.HasDefaultSchema(SchemaNames.Ai);

        // ── Schema overrides for entities created in dbo by existing migrations ──
        // All these entities were created in dbo schema before the Ai module existed.
        // They must continue to use dbo to match the existing database.
        modelBuilder.Entity<AiProvider>().ToTable("AiProviders", SchemaNames.Default);
        modelBuilder.Entity<AiModel>().ToTable("AiModels", SchemaNames.Default);
        modelBuilder.Entity<AiRoutePolicy>().ToTable("AiRoutePolicies", SchemaNames.Default);
        modelBuilder.Entity<PromptSpec>().ToTable("PromptSpecs", SchemaNames.Default);
        modelBuilder.Entity<ToolSpec>().ToTable("ToolSpecs", SchemaNames.Default);
        modelBuilder.Entity<AiPolicy>().ToTable("AiPolicies", SchemaNames.Default);
        modelBuilder.Entity<AiRun>().ToTable("AiRuns", SchemaNames.Default);
        modelBuilder.Entity<AiTrace>().ToTable("AiTraces", SchemaNames.Default);
        modelBuilder.Entity<AiFeedback>().ToTable("AiFeedbacks", SchemaNames.Default);
        modelBuilder.Entity<EvalSuite>().ToTable("EvalSuites", SchemaNames.Default);
        modelBuilder.Entity<EvalRun>().ToTable("EvalRuns", SchemaNames.Default);
        modelBuilder.Entity<Insight>().ToTable("Insights", SchemaNames.Default);
        modelBuilder.Entity<Signal>().ToTable("Signals", SchemaNames.Default);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);

        // AiRoutePolicy has nullable TenantId (global + tenant-specific policies)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiRoutePolicy));
    }
}
