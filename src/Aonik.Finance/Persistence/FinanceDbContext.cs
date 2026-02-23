using Aonik.Domain.Orders.Entities;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Persistence;

/// <summary>
/// Module-scoped DbContext for the Finance domain.
/// Owns Ledger, Payments, Billing, Orders, Pricing, Partners, and PersonalFinance entities.
/// Inherits multi-tenancy enforcement and audit stamping from <see cref="AonikDbContextBase"/>.
///
/// During migration, entities are progressively moved here from AonikDbContext.
/// Both contexts share the same physical SQL Server database.
/// </summary>
internal class FinanceDbContext : AonikDbContextBase
{
    // ── Ledger ─────────────────────────────────────────────────────
    public DbSet<Ledger> Ledgers { get; set; } = null!;
    public DbSet<LedgerAccount> LedgerAccounts { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public DbSet<BalanceSnapshot> BalanceSnapshots { get; set; } = null!;

    // ── Payments ─────────────────────────────────────────────────────
    public DbSet<PaymentIntent> PaymentIntents { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Payout> Payouts { get; set; } = null!;
    public DbSet<Refund> Refunds { get; set; } = null!;
    public DbSet<Chargeback> Chargebacks { get; set; } = null!;

    // ── TEMPORARY: Cross-module DbSets (will be removed when Orders move to Finance) ──
    public DbSet<Order> Orders { get; set; } = null!;

    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All Finance entities use the 'finance' schema by default
        modelBuilder.HasDefaultSchema(SchemaNames.Finance);

        // ── Schema overrides for entities created in dbo by existing migrations ──
        // Ledger entities were created in dbo schema before the Finance module existed.
        // They must continue to use dbo to match the existing database.
        modelBuilder.Entity<Ledger>().ToTable("Ledgers", SchemaNames.Default);
        modelBuilder.Entity<LedgerAccount>().ToTable("LedgerAccounts", SchemaNames.Default);
        modelBuilder.Entity<JournalEntry>().ToTable("JournalEntries", SchemaNames.Default);
        modelBuilder.Entity<JournalEntryLine>().ToTable("JournalEntryLines", SchemaNames.Default);
        modelBuilder.Entity<BalanceSnapshot>().ToTable("BalanceSnapshots", SchemaNames.Default);

        // Payment entities were also created in dbo schema before the Finance module existed.
        modelBuilder.Entity<PaymentIntent>().ToTable("PaymentIntents", SchemaNames.Default);
        modelBuilder.Entity<Payment>().ToTable("Payments", SchemaNames.Default);
        modelBuilder.Entity<Payout>().ToTable("Payouts", SchemaNames.Default);
        modelBuilder.Entity<Refund>().ToTable("Refunds", SchemaNames.Default);
        modelBuilder.Entity<Chargeback>().ToTable("Chargebacks", SchemaNames.Default);

        // TEMPORARY: Cross-module entity schema overrides
        // Order is configured to match AonikDbContext's OrderConfiguration so that
        // InMemory provider can read Order data written by either context.
        // Navigation properties are ignored (their configs live in Infrastructure).
        // Shadow properties must be declared to match the same model shape.
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", SchemaNames.Default);
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.Items);
            entity.Ignore(x => x.PartyRoles);
            entity.Ignore(x => x.HistoryEvents);

            // Shadow properties that exist in AonikDbContext's OrderConfiguration
            entity.Property<string>("OrderNumber").HasMaxLength(64).IsRequired(false);
            entity.Property<string>("ServiceCode").HasMaxLength(50).IsRequired(false);
            entity.Property<string>("MetadataJson").IsRequired(false);
            entity.Property<string>("OrderDetailsJson").IsRequired(false);
        });

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);
    }
}
