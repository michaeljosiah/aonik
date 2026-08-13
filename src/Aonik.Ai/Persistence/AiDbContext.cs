using Aonik.Ai.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Persistence;

/// <summary>
/// Module-scoped DbContext for the AI platform domain.
/// Owns AiProvider, AiModel, AiRoutePolicy, AiPolicy,
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

    // ── LLM Tasks ─────────────────────────────────────────────────
    public DbSet<AiTask> AiTasks { get; set; } = null!;

    // ── Policy & Execution ─────────────────────────────────────────
    public DbSet<AiPolicy> AiPolicies { get; set; } = null!;
    public DbSet<AiRun> AiRuns { get; set; } = null!;
    public DbSet<Entities.Safety.SafetyDecision> SafetyDecisions { get; set; } = null!;
    public DbSet<Entities.Safety.SafetyIncident> SafetyIncidents { get; set; } = null!;
    public DbSet<Entities.Safety.SafetyArtefact> SafetyArtefacts { get; set; } = null!;
    public DbSet<Entities.Safety.SafetyPolicy> SafetyPolicies { get; set; } = null!;
    public DbSet<Entities.Safety.CuratedCharacter> CuratedCharacters { get; set; } = null!;
    public DbSet<Entities.Safety.StoryTemplate> StoryTemplates { get; set; } = null!;
    public DbSet<Entities.Safety.PendingContentReview> PendingContentReviews { get; set; } = null!;
    public DbSet<Entities.Safety.ChildSafetyPreference> ChildSafetyPreferences { get; set; } = null!;
    public DbSet<Entities.Safety.SafetyEscalation> SafetyEscalations { get; set; } = null!;
    public DbSet<Entities.Safety.PreservedMaterialAccess> PreservedMaterialAccesses { get; set; } = null!;
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

    // ── Decision-aware learning (Spec 041) ─────────────────────────
    public DbSet<DecisionPattern> DecisionPatterns { get; set; } = null!;

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

        // AiTask has nullable TenantId (global defaults + tenant-specific overrides)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiTask));
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<AiProvider>(modelBuilder, "AiProviders");
        MapTable<AiModel>(modelBuilder, "AiModels");
        MapTable<AiRoutePolicy>(modelBuilder, "AiRoutePolicies");
        MapTable<AiTask>(modelBuilder, "AiTasks");
        MapTable<AiPolicy>(modelBuilder, "AiPolicies");
        MapTable<AiRun>(modelBuilder, "AiRuns");
        MapTable<Entities.Safety.SafetyDecision>(modelBuilder, "SafetyDecisions");
        MapTable<Entities.Safety.SafetyIncident>(modelBuilder, "SafetyIncidents");
        MapTable<Entities.Safety.SafetyArtefact>(modelBuilder, "SafetyArtefacts");
        MapTable<Entities.Safety.SafetyPolicy>(modelBuilder, "SafetyPolicies");
        MapTable<Entities.Safety.CuratedCharacter>(modelBuilder, "CuratedCharacters");
        MapTable<Entities.Safety.StoryTemplate>(modelBuilder, "StoryTemplates");
        MapTable<Entities.Safety.PendingContentReview>(modelBuilder, "PendingContentReviews");
        MapTable<Entities.Safety.ChildSafetyPreference>(modelBuilder, "ChildSafetyPreferences");
        MapTable<Entities.Safety.SafetyEscalation>(modelBuilder, "SafetyEscalations");
        MapTable<Entities.Safety.PreservedMaterialAccess>(modelBuilder, "PreservedMaterialAccesses");
        MapTable<TenantAgentSettings>(modelBuilder, "TenantAgentSettings");
        MapTable<AiTrace>(modelBuilder, "AiTraces");
        MapTable<AiFeedback>(modelBuilder, "AiFeedbacks");
        MapTable<EvalSuite>(modelBuilder, "EvalSuites");
        MapTable<EvalRun>(modelBuilder, "EvalRuns");
        MapTable<CustomerInsightAiSummary>(modelBuilder, "CustomerInsightAiSummaries");
        MapTable<Insight>(modelBuilder, "Insights");
        MapTable<Signal>(modelBuilder, "Signals");
        MapTable<UserMemoryEntry>(modelBuilder, "UserMemoryEntries");
        MapTable<DecisionPattern>(modelBuilder, "DecisionPatterns");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Ai, tableName);
}
