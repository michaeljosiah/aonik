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

    // ── LLM Tasks ─────────────────────────────────────────────────
    public DbSet<AiTask> AiTasks { get; set; } = null!;

    // ── Policy & Execution ─────────────────────────────────────────
    public DbSet<AiPolicy> AiPolicies { get; set; } = null!;
    public DbSet<AiRun> AiRuns { get; set; } = null!;
    public DbSet<TenantAgentSettings> TenantAgentSettings { get; set; } = null!;
    public DbSet<AiTrace> AiTraces { get; set; } = null!;
    public DbSet<AiFeedback> AiFeedbacks { get; set; } = null!;

    // ── Evaluation ─────────────────────────────────────────────────
    public DbSet<EvalSuite> EvalSuites { get; set; } = null!;
    public DbSet<EvalRun> EvalRuns { get; set; } = null!;

    // ── Insights & Signals ─────────────────────────────────────────
    public DbSet<CustomerInsightAiSummary> CustomerInsightAiSummaries { get; set; } = null!;
    public DbSet<Insight> Insights { get; set; } = null!;
    public DbSet<Signal> Signals { get; set; } = null!;

    // ── User Memory ────────────────────────────────────────────────
    public DbSet<UserMemoryEntry> UserMemoryEntries { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        // Configure RowVersion as optimistic concurrency token on all AuditableEntity types
        ConfigureRowVersions(modelBuilder);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);

        // AiRoutePolicy has nullable TenantId (global + tenant-specific policies)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiRoutePolicy));

        // PromptSpec has nullable TenantId (global defaults + tenant-specific overrides)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(PromptSpec));

        // AiTask has nullable TenantId (global defaults + tenant-specific overrides)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiTask));
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<AiProvider>(modelBuilder, "AiProviders");
        MapTable<AiModel>(modelBuilder, "AiModels");
        MapTable<AiRoutePolicy>(modelBuilder, "AiRoutePolicies");
        MapTable<PromptSpec>(modelBuilder, "PromptSpecs");
        MapTable<AiTask>(modelBuilder, "AiTasks");
        MapTable<ToolSpec>(modelBuilder, "ToolSpecs");
        MapTable<AiPolicy>(modelBuilder, "AiPolicies");
        MapTable<AiRun>(modelBuilder, "AiRuns");
        MapTable<TenantAgentSettings>(modelBuilder, "TenantAgentSettings");
        MapTable<AiTrace>(modelBuilder, "AiTraces");
        MapTable<AiFeedback>(modelBuilder, "AiFeedbacks");
        MapTable<EvalSuite>(modelBuilder, "EvalSuites");
        MapTable<EvalRun>(modelBuilder, "EvalRuns");
        MapTable<CustomerInsightAiSummary>(modelBuilder, "CustomerInsightAiSummaries");
        MapTable<Insight>(modelBuilder, "Insights");
        MapTable<Signal>(modelBuilder, "Signals");
        MapTable<UserMemoryEntry>(modelBuilder, "UserMemoryEntries");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Ai}{tableName}", SchemaNames.Default);
    }
}
