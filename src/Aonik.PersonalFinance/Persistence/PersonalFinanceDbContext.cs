using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Persistence;

/// <summary>
/// Module-scoped DbContext for the PersonalFinance domain (Spec 027 Phase 2).
/// Owns Households, PersonalAccounts, PersonalTransactions, Bills,
/// Subscriptions, DebtRepayments, Budgets, Goals, FinancialContext,
/// FinancialLifeGraph, CustomerInsight, StatementImports, FinancialConnection.
///
/// Shares the same physical SQL Server database as <c>AonikDbContext</c> (the
/// canonical migration stream) and <c>FinanceDbContext</c>. Module DbContexts
/// are runtime-only DI scoping — the migration stream stays in AonikDbContext
/// per <a href="../../../docs/decisions/005-adopt-module-first-modular-monolith.md">ADR-005</a>.
///
/// Subsequent phases (3+) migrate the PF services off <c>FinanceDbContext</c>
/// onto this context.
/// </summary>
internal sealed class PersonalFinanceDbContext : AonikDbContextBase
{
    // ── PersonalFinance entities ─────────────────────────────────────────
    public DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public DbSet<Household> Households { get; set; } = null!;
    public DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public DbSet<PersonalLinkedAccount> PersonalLinkedAccounts { get; set; } = null!;
    public DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public DbSet<TransactionCategory> TransactionCategories { get; set; } = null!;
    public DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public DbSet<TransactionAttachment> TransactionAttachments { get; set; } = null!;
    public DbSet<Bill> Bills { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<PersonalRecurringBill> PersonalRecurringBills { get; set; } = null!;
    public DbSet<DebtRepayment> DebtRepayments { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public DbSet<Goal> Goals { get; set; } = null!;
    public DbSet<CompassPlan> CompassPlans { get; set; } = null!;
    public DbSet<CareEntity> CareEntities { get; set; } = null!;
    public DbSet<PaymentLog> PaymentLogs { get; set; } = null!;
    public DbSet<CommitmentCycle> CommitmentCycles { get; set; } = null!;
    public DbSet<CircleGrant> CircleGrants { get; set; } = null!;
    public DbSet<CircleInvite> CircleInvites { get; set; } = null!;
    public DbSet<FinancialConnection> FinancialConnections { get; set; } = null!;
    public DbSet<FinancialConnectionSession> FinancialConnectionSessions { get; set; } = null!;
    public DbSet<FinancialWebhookEvent> FinancialWebhookEvents { get; set; } = null!;
    public DbSet<FinancialContext> FinancialContexts { get; set; } = null!;
    public DbSet<FinancialContextFundingSource> FinancialContextFundingSources { get; set; } = null!;
    public DbSet<FinancialLifeGraphNode> FinancialLifeGraphNodes { get; set; } = null!;
    public DbSet<FinancialLifeGraphEdge> FinancialLifeGraphEdges { get; set; } = null!;
    public DbSet<CustomerInsightSnapshot> CustomerInsightSnapshots { get; set; } = null!;
    public DbSet<StatementImport> StatementImports { get; set; } = null!;
    public DbSet<StatementImportRow> StatementImportRows { get; set; } = null!;

    // NOTE (Spec 027 Phase 2): The spec also lists Platform read models
    // (Parties / Users / etc) on this context. Those types currently live in
    // Aonik.Finance.Entities and are not yet relocated, so adding them here
    // would require a PersonalFinance -> Finance reference that the spec
    // forbids. PF services still use FinanceDbContext for those joins until
    // Phase 3 relocates the read models to a neutral assembly.

    public PersonalFinanceDbContext(
        DbContextOptions<PersonalFinanceDbContext> options,
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

        // Apply PersonalFinance EF configurations from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonalFinanceDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        ConfigureRowVersions(modelBuilder);

        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// CategorisationRule with <c>Scope == "System"</c> is global (tenant-less).
    /// Mirrors the same carve-out in <c>FinanceDbContext.IsGlobalEntity</c>.
    /// </summary>
    protected override bool IsGlobalEntity(object entity)
    {
        if (base.IsGlobalEntity(entity))
            return true;

        return entity is CategorisationRule rule
            && string.Equals(rule.Scope, "System", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<PersonalProfile>(modelBuilder, "PersonalProfiles");
        MapTable<Household>(modelBuilder, "Households");
        MapTable<HouseholdMember>(modelBuilder, "HouseholdMembers");
        MapTable<PersonalAccount>(modelBuilder, "PersonalAccounts");
        MapTable<PersonalLinkedAccount>(modelBuilder, "PersonalLinkedAccounts");
        MapTable<PersonalTransaction>(modelBuilder, "PersonalTransactions");
        MapTable<TransactionCategory>(modelBuilder, "TransactionCategories");
        MapTable<CategorisationRule>(modelBuilder, "CategorisationRules");
        MapTable<TransactionAttachment>(modelBuilder, "TransactionAttachments");
        MapTable<Bill>(modelBuilder, "Bills");
        MapTable<Subscription>(modelBuilder, "Subscriptions");
        MapTable<PersonalRecurringBill>(modelBuilder, "PersonalRecurringBills");
        MapTable<DebtRepayment>(modelBuilder, "DebtRepayments");
        MapTable<Budget>(modelBuilder, "Budgets");
        MapTable<BudgetLine>(modelBuilder, "BudgetLines");
        MapTable<Goal>(modelBuilder, "Goals");
        MapTable<CompassPlan>(modelBuilder, "CompassPlans");
        MapTable<CareEntity>(modelBuilder, "CareEntities");
        MapTable<PaymentLog>(modelBuilder, "PaymentLogs");
        MapTable<CommitmentCycle>(modelBuilder, "CommitmentCycles");
        MapTable<CircleGrant>(modelBuilder, "CircleGrants");
        MapTable<CircleInvite>(modelBuilder, "CircleInvites");
        MapTable<FinancialConnection>(modelBuilder, "FinancialConnections");
        MapTable<FinancialConnectionSession>(modelBuilder, "FinancialConnectionSessions");
        MapTable<FinancialWebhookEvent>(modelBuilder, "FinancialWebhookEvents");
        MapTable<FinancialContext>(modelBuilder, "FinancialContexts");
        MapTable<FinancialContextFundingSource>(modelBuilder, "FinancialContextFundingSources");
        MapTable<FinancialLifeGraphNode>(modelBuilder, "FinancialLifeGraphNodes");
        MapTable<FinancialLifeGraphEdge>(modelBuilder, "FinancialLifeGraphEdges");
        MapTable<CustomerInsightSnapshot>(modelBuilder, "CustomerInsightSnapshots");
        MapTable<StatementImport>(modelBuilder, "StatementImports");
        MapTable<StatementImportRow>(modelBuilder, "StatementImportRows");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Finance}{tableName}", SchemaNames.Default);
}
