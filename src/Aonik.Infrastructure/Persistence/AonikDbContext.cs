using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Aonik.Infrastructure.Persistence;

public class AonikDbContext : DbContext, IAonikDbContext
{
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<Signal> Signals => Set<Signal>();

    public AonikDbContext(DbContextOptions<AonikDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
